// §803 provenance: item ORML-137, pass FULL, round 1. Reviewer-authored proposal, placed unchanged except for the
// re-assertion the TL ruled (ADR e8c50bcc v5 D12.1, fix list a3a56082 on ORML-137): the AsyncLocal holder mark does not
// follow a unit of work handed back from an async helper or Task.Run, so the call is not refused; it returns Busy within
// its budget plus the fixture margin, throws no InvalidOperationException, and the gate is free once the unit ends.
using System.Diagnostics;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class WalHolderHandoffAttackTests
{
    [Theory(DisplayName =
        "F1 803 FULL round 1: returned UoW retains holder refusal")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Returned_unit_must_not_wait_on_its_own_gate(bool childTask)
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        var (context, source) = db.Counting();

        var unit = await (
            childTask
                ? Task.Run(() => OpenAsync(db.Context))
                : OpenAsync(db.Context));

        WalCheckpointResult result;
        var clock = Stopwatch.StartNew();
        try
        {
            db.Sink.Clear();

            result = await context.Maintenance.CheckpointWalAsync(
                WalFixture.Request(WalCheckpointMode.Truncate, 200));
            clock.Stop();
        }
        finally
        {
            await unit.DisposeAsync();
        }

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.True(
            clock.Elapsed < TimeSpan.FromMilliseconds(200) + SqliteMaintenanceDatabase.Margin,
            "The call waited " + clock.Elapsed + ", past its 200 ms budget plus the margin.");
        Assert.Equal(0, source.Opens);
        Assert.Empty(db.Sink.Maintenance);

        // The gate is free once the unit of work has ended.
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
    }

    private static async Task<UnitOfWork> OpenAsync(OrmContext context) =>
        await UnitOfWork.BeginAsync(context);
}
