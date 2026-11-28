using Xunit;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ExecutionRig;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ScriptedProvider;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// ADR e8c50bcc v7 D14.1 C1 (D14.5 clause theory): one execution per public call. Through the public port on scripted
// doubles and the manual clock; nothing sleeps and nothing reads real time.
public sealed class OneExecutionTests
{
    [Theory(DisplayName = "ORML-137 D14 C1 one execution: a composite spends one budget, the first gate is released "
        + "before the second, one deadline")]
    [InlineData(1000)]
    [InlineData(null)]
    public async Task C1_OneExecution(int? timeoutMs)
    {
        var rig = new ExecutionRig { Provider = { MaintenanceTimeout = TimeSpan.FromSeconds(30) } };
        var total = timeoutMs is { } ms ? Ms(ms) : TimeSpan.FromSeconds(30);
        rig.Source.Script(Checkpoint, _ =>
        {
            rig.Clock.Advance(Ms(300));
            return Row(rig.Source, 0, 4, 4);
        });
        var request = new VacuumRequest(VacuumMode.Plain) { Timeout = timeoutMs is { } given ? Ms(given) : null };

        var result = await rig.Maintenance.VacuumAsync(request, rig.Caller.Token);

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        Assert.Equal(MaintenanceStatus.Completed, result.Checkpoint!.Status);
        // The second gate gets only what the checkpoint left; the first was released before it was asked for.
        Assert.Equal([total, total - Ms(300)], rig.Provider.GateTimeouts);
        Assert.Equal(["gate", "release gate", "gate", "release gate"], rig.Log.Entries.Where(e => e.Contains("gate")));
        // One budget: one deadline timer for both opens, and one event per statement.
        Assert.Single(rig.Clock.Timers);
        MaintenanceOperation?[] events = [MaintenanceOperation.WalCheckpoint, MaintenanceOperation.Vacuum];
        Assert.Equal(events, rig.Sink.Maintenance.Select(e => e.Operation));
        rig.AssertUnwound();
    }
}
