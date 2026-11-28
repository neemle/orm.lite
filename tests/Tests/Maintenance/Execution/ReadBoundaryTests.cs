using Xunit;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ExecutionRig;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ScriptedProvider;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// ADR e8c50bcc v7 D14.1 C5 (D14.5 clause theory): the budget is admission only, so an admitted statement keeps its
// result however long reading it takes; cancellation is arbitrated after every read and again at the root.
public sealed class ReadBoundaryTests
{
    private static readonly Dictionary<string, Func<ExecutionRig, Task>> Cases = new()
    {
        // Cancelled as the read completes: the row is not handed to the reducer.
        ["read"] = async rig =>
        {
            rig.Source.Script(Integrity, _ =>
                new ScriptedReader(rig.Source, [["bad"]]) { OnRead = (_, _) => rig.Caller.CancelAsync() });
            await rig.CancelledAsync(rig.CheckIntegrityAsync(), null);
            Assert.Equal(0, rig.Provider.Findings);
        },
        ["accept"] = async rig =>
        {
            rig.Source.Script(Integrity, _ => rig.Rows(["bad"], ["ok"]));
            rig.Provider.OnFinding = rig.Caller.Cancel;
            await rig.CancelledAsync(rig.CheckIntegrityAsync(), null);
        },
        ["complete"] = async rig =>
        {
            rig.Source.Script(Integrity, _ => rig.Rows(["ok"]));
            rig.Provider.OnCap = rig.Caller.Cancel;
            await rig.CancelledAsync(rig.CheckIntegrityAsync(), null);
        },
        // The checkpoint was admitted; reading its row outlasts the budget. Its result stands, and VACUUM is denied.
        ["expire"] = async rig =>
        {
            rig.Source.Script(Checkpoint, _ => new ScriptedReader(rig.Source, [[0L, 4L, 4L]])
            {
                OnRead = (_, _) =>
                {
                    rig.Clock.Advance(Ms(1000));
                    return Task.CompletedTask;
                },
            });
            var result = await rig.VacuumAsync();
            Assert.Equal(MaintenanceStatus.Busy, result.Status);
            Assert.Equal(MaintenanceStatus.Completed, result.Checkpoint!.Status);
            Assert.Empty(rig.Source.Of(Vacuum));
            Assert.Equal(1, rig.Log.Count("gate"));
        },
    };

    [Theory(DisplayName = "ORML-137 D14 C5 read boundary: a cancel while reading is cancellation; the budget never "
        + "ends an admitted statement")]
    [InlineData("read")]
    [InlineData("accept")]
    [InlineData("complete")]
    [InlineData("expire")]
    public async Task C5_ReadBoundary(string at)
    {
        var rig = new ExecutionRig();

        await Cases[at](rig);

        rig.AssertUnwound();
    }
}
