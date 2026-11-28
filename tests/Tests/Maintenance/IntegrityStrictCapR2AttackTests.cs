// §803 provenance: item ORML-138, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class IntegrityStrictCapR2AttackTests
{
    [Theory(DisplayName =
        "R2-F4 803 FULL round 2: capped table checks must accept MaxFindings above 100")]
    [InlineData(IntegrityCheckMode.Full)]
    [InlineData(IntegrityCheckMode.Quick)]
    public async Task Above_cap_is_accepted_and_reported_as_truncated(
        IntegrityCheckMode mode)
    {
        await using var db =
            await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();

        if (mode == IntegrityCheckMode.Full)
        {
            await IntegrityFixtures.CorruptIndexAsync(db, rows: 200);
        }
        else
        {
            await using var raw = await db.RawAsync();
            await SqliteMaintenanceDatabase.Exec(
                raw, "CREATE TABLE t (v INTEGER CHECK (v > 0))");
            await SqliteMaintenanceDatabase.Exec(
                raw, "PRAGMA ignore_check_constraints=ON");
            await SqliteMaintenanceDatabase.Exec(raw,
                """
                WITH RECURSIVE numbers(n) AS (
                    SELECT 1
                    UNION ALL
                    SELECT n + 1 FROM numbers WHERE n < 200
                )
                INSERT INTO t(v) SELECT -n FROM numbers
                """);
            await SqliteMaintenanceDatabase.Exec(
                raw, "PRAGMA ignore_check_constraints=OFF");
        }

        var whole = await db.Maintenance.CheckIntegrityAsync(
            IntegrityFixtures.Request(mode, maxFindings: 1000));
        Assert.True(whole.Findings.Count > 100);
        Assert.False(whole.Truncated);
        db.Sink.Clear();

        // No catch: rejecting this valid request violates D12.6.
        var scoped = await db.Maintenance.CheckIntegrityAsync(
            IntegrityFixtures.Request(
                mode, Descriptors.Table("t"), maxFindings: 1000));

        Assert.Equal(MaintenanceStatus.IssuesFound, scoped.Status);
        Assert.Equal(100, scoped.Findings.Count);
        Assert.True(scoped.Truncated);
        Assert.Equal(
            MaintenanceOperation.IntegrityCheck,
            Assert.Single(db.Sink.Maintenance).Operation);
    }
}
