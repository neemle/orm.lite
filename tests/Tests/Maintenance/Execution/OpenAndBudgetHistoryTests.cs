using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ExecutionRig;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ScriptedProvider;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// ADR e8c50bcc v7 D14.4/D14.5 historical regressions, one row per finding id of the section 803 rounds 1-3 on
// ORML-134..138: the late open, the spent budget before a start, and the open's bound. Replayed deterministically on
// scripted doubles and the manual clock; the native attack tests stay as they are.
public sealed class OpenAndBudgetHistoryTests
{
    private static readonly Dictionary<string, Func<ExecutionRig, Task<MaintenanceStatus>>> LateOpenCalls = new()
    {
        ["137-R2-F1"] = async rig => (await rig.CheckpointAsync()).Status,
        ["138-R2-F1"] = async rig => (await rig.CheckIntegrityAsync()).Status,
        ["136-R2-F2-open"] = async rig => (await rig.ReindexIndexAsync()).Status,
        ["134-R2-F1"] = rig => VacuumStatus(rig, null),
        ["134-vacuum-second-open"] = rig => VacuumStatus(rig, MaintenanceStatus.Completed),
    };

    private static readonly Dictionary<string, Func<ExecutionRig, Task>> SpentCases = new()
    {
        ["134-F2"] = async rig =>
        {
            rig.Source.Script(Checkpoint, _ => Spend(rig, Row(rig.Source, 0, 4, 4)));
            var vacuum = await rig.VacuumAsync();
            Assert.Equal(MaintenanceStatus.Busy, vacuum.Status);
            Assert.Equal(MaintenanceStatus.Completed, vacuum.Checkpoint!.Status);
            Assert.Empty(rig.Source.Of(Vacuum));
            Assert.Equal(1, rig.Log.Count("gate"));
        },
        ["135-R2-F2"] = async rig =>
        {
            rig.Source.Script(Set, _ => Spend(rig, 0));
            Assert.Equal(MaintenanceStatus.Busy, (await rig.AnalyzeAsync()).Status);
            Assert.Empty(rig.Source.Of(Analyze));
        },
        ["136-R2-F2-lookup"] = async rig =>
        {
            rig.Source.Script(Lookup, _ => Spend(rig, "s"));
            Assert.Equal(MaintenanceStatus.Busy, (await rig.ReindexIndexAsync()).Status);
            Assert.Empty(rig.Source.Of(Reindex + " s"));
        },
        // The setup spends the budget; a trap lookup must not run, so its failure cannot replace the Busy.
        ["136-R3-F2"] = async rig =>
        {
            rig.Source.Script(Set, _ => Spend(rig, 0));
            rig.Source.Script(Lookup, _ => throw new InvalidOperationException("the trap lookup ran"));
            Assert.Equal(MaintenanceStatus.Busy, (await rig.ReindexIndexAsync()).Status);
            Assert.Empty(rig.Source.Of(Lookup));
        },
    };

    [Theory(DisplayName = "ORML-137 D14 history LateOpen: an open that completes after the budget ran out starts "
        + "nothing")]
    [InlineData("137-R2-F1")]
    [InlineData("134-R2-F1")]
    [InlineData("138-R2-F1")]
    [InlineData("136-R2-F2-open")]
    [InlineData("134-vacuum-second-open")]
    public async Task LateOpen(string finding)
    {
        var rig = new ExecutionRig { Provider = { Guarded = true, LooksUp = true } }.WithCompletedCheckpoint();
        var second = finding == "134-vacuum-second-open";
        if (finding == "134-R2-F1") rig.Provider.Capabilities = Without(Capability.WalCheckpoint);
        // The open ignores its token and completes after the deadline fired.
        rig.Source.OnOpen = _ =>
        {
            if (!second || rig.Source.Connections.Count == 2) rig.Clock.Advance(Ms(1000));
            return Task.CompletedTask;
        };

        Assert.Equal(MaintenanceStatus.Busy, await LateOpenCalls[finding](rig));

        Assert.Equal(second ? 1 : 0, rig.Source.Of(Set).Count);
        Assert.Empty(rig.Source.Of(Lookup));
        Assert.Equal(second ? 1 : 0, rig.Sink.Maintenance.Count);
        rig.AssertUnwound();
    }

    [Theory(DisplayName = "ORML-137 D14 history SpentBeforeStart: an effect that spends the budget starts nothing "
        + "after it")]
    [InlineData("134-F2")]
    [InlineData("135-R2-F2")]
    [InlineData("136-R2-F2-lookup")]
    [InlineData("136-R3-F2")]
    public async Task SpentBeforeStart(string finding)
    {
        var rig = new ExecutionRig { Provider = { Guarded = finding != "134-F2", LooksUp = true } };

        await SpentCases[finding](rig);

        Assert.DoesNotContain(rig.Sink.Maintenance, e => e.Operation != MaintenanceOperation.WalCheckpoint);
        rig.AssertUnwound();
    }

    [Theory(DisplayName = "ORML-137 D14 history OpenBoundary: the open is bounded by the budget, and its contention is "
        + "Busy")]
    [InlineData("137-F2", 0)]
    [InlineData("137-F3", 5)]
    [InlineData("134-F6", 6)]
    public async Task OpenBoundary(string finding, int code)
    {
        var rig = new ExecutionRig();
        rig.Provider.Classify = ((IMaintenanceProvider)SqliteProvider.Instance).IsContention;
        rig.Source.OnOpen = code == 0
            ? WaitForTokenAsync
            : _ => throw new SqliteException("database is locked while opening", code);

        var call = finding == "134-F6" ? StatusOf(rig.VacuumAsync()) : StatusOf(rig.CheckpointAsync());
        if (code == 0)
        {
            rig.Clock.Advance(Ms(999));
            Assert.False(call.IsCompleted);
            rig.Clock.Advance(Ms(1));
        }

        Assert.Equal(MaintenanceStatus.Busy, await call);
        Assert.Empty(rig.Source.Dispatches);
        rig.AssertUnwound();
    }

    // An open that would wait for ever: it ends only when its token does.
    internal static async Task WaitForTokenAsync(CancellationToken token)
    {
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using (token.Register(() => ended.SetResult())) await ended.Task;
        token.ThrowIfCancellationRequested();
    }

    internal static async Task<MaintenanceStatus> StatusOf<TResult>(Task<TResult> call)
        where TResult : IMaintenanceResult =>
        (await call).Status;

    internal static CapabilitySet Without(Capability capability) =>
        CapabilitySet.Everything().With(capability, Support.None);

    private static async Task<MaintenanceStatus> VacuumStatus(ExecutionRig rig, MaintenanceStatus? checkpoint)
    {
        var result = await rig.VacuumAsync();
        Assert.Equal(checkpoint, result.Checkpoint?.Status);
        Assert.Empty(rig.Source.Of(Vacuum));
        return result.Status;
    }

    // The effect's answer, after it spent the whole budget.
    private static object? Spend(ExecutionRig rig, object? answer)
    {
        rig.Clock.Advance(Ms(1000));
        return answer;
    }
}
