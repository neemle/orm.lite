// §803 provenance: item ORML-138, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class IntegrityNumericScopeAttackTests
{
    [Theory(DisplayName =
        "F1 803 FULL round 1: numeric table targets must not silently check unrelated tables")]
    [InlineData(IntegrityCheckMode.Full)]
    [InlineData(IntegrityCheckMode.Quick)]
    public async Task Numeric_target_preserves_scope_or_is_explicitly_refused(
        IntegrityCheckMode mode)
    {
        await using var db =
            await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();

        await db.ExecAsync("CREATE TABLE \"100\" (id INTEGER PRIMARY KEY)");
        await db.ExecAsync("INSERT INTO \"100\" VALUES (1)");

        if (mode == IntegrityCheckMode.Full)
        {
            await IntegrityFixtures.CorruptIndexAsync(db);
        }
        else
        {
            await using var raw = await db.RawAsync();
            await SqliteMaintenanceDatabase.Exec(
                raw, "CREATE TABLE damaged (v INTEGER CHECK (v > 0))");
            await SqliteMaintenanceDatabase.Exec(
                raw, "PRAGMA ignore_check_constraints=ON");
            await SqliteMaintenanceDatabase.Exec(
                raw, "INSERT INTO damaged VALUES (-1)");
            await SqliteMaintenanceDatabase.Exec(
                raw, "PRAGMA ignore_check_constraints=OFF");
        }

        // Establish that the unrelated damage is visible to this mode.
        var whole = await db.Maintenance.CheckIntegrityAsync(
            IntegrityFixtures.Request(mode, maxFindings: 10));

        Assert.Equal(MaintenanceStatus.IssuesFound, whole.Status);
        Assert.NotEmpty(whole.Findings);
        db.Sink.Clear();

        IntegrityCheckResult scoped;
        try
        {
            scoped = await db.Maintenance.CheckIntegrityAsync(
                IntegrityFixtures.Request(
                    mode, Descriptors.Table("100"), maxFindings: 3));
        }
        catch (ArgumentException)
        {
            // An explicitly documented restriction is a fail-closed fix.
            Assert.Empty(db.Sink.Maintenance);
            return;
        }

        Assert.Equal(MaintenanceStatus.Completed, scoped.Status);
        Assert.Empty(scoped.Findings);
        Assert.False(scoped.Truncated);
    }
}
