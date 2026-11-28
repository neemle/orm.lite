using Xunit;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ExecutionRig;
using static Neemle.OrmLite.Tests.Maintenance.Execution.OpenAndBudgetHistoryTests;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ScriptedProvider;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// ADR e8c50bcc v7 D14.4/D14.5 historical regressions, one row per finding id: a cancel between the composite's steps
// or while mapping rows, and the open timer's rounding (QA survivor M137-R3b3).
public sealed class CompositeHistoryTests
{
    private static readonly Dictionary<string, Func<ExecutionRig, Task>> Cases = new()
    {
        ["134-R3-F1-gate-busy"] = rig =>
        {
            rig.Provider.Gate = (_, _) =>
            {
                rig.Caller.Cancel();
                return ValueTask.FromResult(new MaintenanceWriteLease(false, null));
            };
            return rig.CancelledAsync(rig.VacuumAsync(), null);
        },
        ["134-R3-F1-busy-row"] = rig => CancelBetweenSteps(rig, Row(rig.Source, 1, 4, 2)),
        ["134-R3-F1-partial"] = rig => CancelBetweenSteps(rig, Row(rig.Source, 0, 4, 2)),
        ["134-R3-F1-completed"] = rig => CancelBetweenSteps(rig, Row(rig.Source, 0, 4, 4)),
        ["138-R2-F2a"] = rig =>
        {
            rig.Source.Script(Integrity, _ => rig.Rows(["page 3 is never used"]));
            rig.Provider.OnFinding = rig.Caller.Cancel;
            return rig.CancelledAsync(rig.CheckIntegrityAsync(), null);
        },
        ["M137-R3b3"] = TimerRoundingAsync,
    };

    [Theory(DisplayName = "ORML-137 D14 history CompositeAndMapping: a cancel between composite steps or while mapping "
        + "rows is cancellation")]
    [InlineData("134-R3-F1-gate-busy")]
    [InlineData("134-R3-F1-busy-row")]
    [InlineData("134-R3-F1-partial")]
    [InlineData("134-R3-F1-completed")]
    [InlineData("138-R2-F2a")]
    [InlineData("M137-R3b3")]
    public async Task CompositeAndMapping(string finding)
    {
        var rig = new ExecutionRig();

        await Cases[finding](rig);

        Assert.Empty(rig.Source.Of(Vacuum));
        rig.AssertUnwound();
    }

    // The checkpoint finished with a row; the cancel lands as its gate is released, before VACUUM would resume.
    private static async Task CancelBetweenSteps(ExecutionRig rig, ScriptedReader row)
    {
        rig.Source.Script(Checkpoint, _ => row);
        rig.Provider.OnRelease = rig.Caller.CancelAsync;
        await rig.CancelledAsync(rig.VacuumAsync(), null);
        Assert.Equal(1, rig.Log.Count("gate"));
    }

    // The timer is armed at the remainder rounded UP: at 400.5 ms of a 400.5 ms budget nothing has fired yet; at 401 ms
    // the open's token is cancelled and the open is Busy.
    private static async Task TimerRoundingAsync(ExecutionRig rig)
    {
        rig.Source.OnOpen = WaitForTokenAsync;
        var request = new WalCheckpointRequest(WalCheckpointMode.Truncate) { Timeout = TimeSpan.FromTicks(4_005_000) };
        var call = rig.Maintenance.CheckpointWalAsync(request, rig.Caller.Token);
        rig.Clock.Advance(Ms(400.5));
        Assert.False(call.IsCompleted);
        rig.Clock.Advance(Ms(0.5));
        Assert.Equal(MaintenanceStatus.Busy, (await call).Status);
        Assert.Equal([Ms(401)], rig.Clock.Timer.Arms);
    }
}
