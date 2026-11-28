// §803 provenance: item ORML-138, pass FULL, round 1. Reviewer-authored proposal, placed unchanged except for the
// re-assertion the TL ruled (ADR e8c50bcc v5 D12.6, fix list a3a56082 on ORML-137): SQLite caps a table-scoped Full or
// Quick check at 100 messages, MaxFindings above 100 is accepted, and the capped list is reported as Truncated. The
// last two assertions read Truncated = true and min(MaxFindings, 100) findings, where they read
// all.Findings.Count > MaxFindings and min(MaxFindings, all.Findings.Count).
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class IntegrityTableLimitAttackTests
{
    [Theory(DisplayName =
        "F2 803 FULL round 1: table checks must not silently exhaust the native error limit")]
    [InlineData(IntegrityCheckMode.Full, 100)]
    [InlineData(IntegrityCheckMode.Full, 1000)]
    [InlineData(IntegrityCheckMode.Quick, 100)]
    [InlineData(IntegrityCheckMode.Quick, 1000)]
    public async Task Table_results_are_complete_or_explicitly_refused(
        IntegrityCheckMode mode, int maxFindings)
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
            await SqliteMaintenanceDatabase.Exec(
                raw,
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

        // Only t is damaged. Establish the actual reporting-row count
        // without relying on a hard-coded number of index messages.
        var all = await db.Maintenance.CheckIntegrityAsync(
            IntegrityFixtures.Request(
                mode, maxFindings: IntegrityCheckRequest.MaxFindingsLimit));

        Assert.Equal(MaintenanceStatus.IssuesFound, all.Status);
        Assert.False(all.Truncated);
        Assert.True(all.Findings.Count > 100);
        db.Sink.Clear();

        IntegrityCheckResult scoped;
        try
        {
            scoped = await db.Maintenance.CheckIntegrityAsync(
                IntegrityFixtures.Request(
                    mode, Descriptors.Table("t"), maxFindings: maxFindings));
        }
        catch (ArgumentException)
        {
            // Permitted only with an explicit contract restriction.
            Assert.Empty(db.Sink.Maintenance);
            return;
        }

        Assert.Equal(MaintenanceStatus.IssuesFound, scoped.Status);
        Assert.Equal(
            Math.Min(maxFindings, 100),
            scoped.Findings.Count);
        Assert.True(scoped.Truncated);
    }
}
