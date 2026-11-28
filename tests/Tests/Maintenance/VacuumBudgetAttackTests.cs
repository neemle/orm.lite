// §803 provenance: item ORML-134, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class VacuumBudgetAttackTests
{
    [Fact(DisplayName =
        "F2 803 FULL round 1: an exhausted composite budget cannot start VACUUM")]
    public async Task Exhausted_budget_prevents_the_second_statement()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync(WalFixture.Items);
        db.Sink.Clear();

        using var hook = new StatementStartHook();
        var starts = 0;

        try
        {
            StatementStartHook.OnStart(() =>
            {
                if (Interlocked.Increment(ref starts) == 1)
                    Thread.Sleep(600);
            });

            var result = await db.Maintenance.VacuumAsync(
                VacuumFixture.Plain(200));

            Assert.Equal(
                MaintenanceStatus.Completed,
                result.Checkpoint?.Status);
            Assert.DoesNotContain(
                nameof(MaintenanceOperation.Vacuum),
                db.Sink.Operations);
            Assert.Equal(MaintenanceStatus.Busy, result.Status);
        }
        finally
        {
            StatementStartHook.OnStart(null);
        }
    }
}
