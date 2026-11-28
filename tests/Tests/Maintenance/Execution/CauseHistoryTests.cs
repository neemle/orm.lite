using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ExecutionRig;
using static Neemle.OrmLite.Tests.Maintenance.Execution.OpenAndBudgetHistoryTests;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ScriptedProvider;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// ADR e8c50bcc v7 D14.4/D14.5 historical regressions, one row per finding id: the cause through classification, the
// retry boundary, a cancel during a native busy wait, and the cause through cleanup.
public sealed class CauseHistoryTests
{
    private static readonly Dictionary<string, Func<ExecutionRig, Task<MaintenanceStatus>>> Calls = new()
    {
        ["137"] = rig => StatusOf(rig.CheckpointAsync(5000)),
        ["135"] = rig => StatusOf(rig.AnalyzeAsync(5000)),
        ["136"] = rig => StatusOf(rig.ReindexIndexAsync(5000)),
        ["138"] = rig => StatusOf(rig.CheckIntegrityAsync(5000)),
    };

    private static readonly string[] Statements = [Checkpoint, Analyze, Reindex, Integrity];

    [Theory(DisplayName = "ORML-137 D14 history ClassificationCause: a cancel during classification keeps the "
        + "contention as the cause")]
    [InlineData("137-R2-F2")]
    [InlineData("135-R2-F1")]
    [InlineData("136-R2-F3")]
    [InlineData("138-R2-F2b")]
    public async Task ClassificationCause(string finding)
    {
        var rig = new ExecutionRig();
        var busy = new ScriptedDbException("busy", true);
        foreach (var text in Statements) rig.Source.Script(text, _ => throw busy);
        rig.Provider.Classify = _ =>
        {
            rig.Caller.Cancel();
            return true;
        };

        await rig.CancelledAsync(Calls[finding[..3]](rig), busy);

        Assert.Equal(1, rig.Provider.Classifications);
        rig.AssertUnwound();
    }

    [Theory(DisplayName = "ORML-137 D14 history RetryBoundary: no retry after a cancel or an expiry in classification")]
    [InlineData("137-R3-F1")]
    [InlineData("136-R3-F1")]
    [InlineData("138-R3-F1")]
    [InlineData("138-R3-F2")]
    public async Task RetryBoundary(string finding)
    {
        var rig = new ExecutionRig { Provider = { ContentionRetrySlice = TimeSpan.FromSeconds(1) } };
        var busy = new ScriptedDbException("busy", true);
        var trap = new InvalidOperationException("a second attempt was dispatched");
        foreach (var text in Statements) rig.Source.Script(text, d => throw (d.Attempt == 1 ? busy : trap));
        var expire = finding == "138-R3-F2";
        rig.Provider.Classify = _ =>
        {
            if (expire) rig.Clock.Skip(Ms(5000));
            else rig.Caller.Cancel();
            return true;
        };

        var call = Calls[finding[..3]](rig);

        if (expire) Assert.Equal(MaintenanceStatus.Busy, await call);
        else await rig.CancelledAsync(call, busy);
        Assert.Single(rig.Source.Dispatches);
        Assert.Same(busy, Assert.Single(rig.Sink.Maintenance).Error);
        rig.AssertUnwound();
    }

    [Theory(DisplayName = "ORML-137 D14 history NativeWaitCancel: a cancel during a native busy wait is cancellation "
        + "once the slice ends")]
    [InlineData("137-F4")]
    [InlineData("135-F2")]
    [InlineData("136-F2")]
    public async Task NativeWaitCancel(string finding)
    {
        var rig = new ExecutionRig { Provider = { ContentionRetrySlice = TimeSpan.FromSeconds(1) } };
        var busy = new SqliteException("database is locked", 5);
        rig.Provider.Classify = ((IMaintenanceProvider)SqliteProvider.Instance).IsContention;
        // The driver's busy loop does not see the token: the cancel at 300 ms is noticed when the slice ends.
        foreach (var text in Statements)
            rig.Source.Script(text, dispatch =>
            {
                Assert.Equal(1, dispatch.CommandTimeout);
                rig.Clock.Skip(Ms(300));
                rig.Caller.Cancel();
                rig.Clock.Skip(Ms(700));
                throw busy;
            });

        await rig.CancelledAsync(Calls[finding[..3]](rig), busy);

        Assert.Equal(Ms(1000), rig.Clock.Now);
        Assert.Single(rig.Source.Dispatches);
        Assert.Equal(0, rig.Provider.Classifications);
        rig.AssertUnwound();
    }

    [Theory(DisplayName = "ORML-137 D14 history CleanupCause: the teardown is bounded, and a cancel during it keeps "
        + "the cause")]
    [InlineData("134-F3")]
    [InlineData("134-R2-F2")]
    public async Task CleanupCause(string finding)
    {
        var rig = new ExecutionRig { Provider = { Guarded = true, Capabilities = Without(Capability.WalCheckpoint) } };
        var busy = new ScriptedDbException("lock timeout", true);
        if (finding == "134-R2-F2")
        {
            rig.Source.Script(Vacuum, _ => throw busy);
            rig.Source.Script(Reset, _ =>
            {
                rig.Caller.Cancel();
                return 0;
            });
            await rig.CancelledAsync(rig.VacuumAsync(), busy);
        }
        else Assert.Equal(MaintenanceStatus.Completed, (await rig.VacuumAsync()).Status);

        var reset = Assert.Single(rig.Source.Of(Reset));
        Assert.Equal(1, reset.CommandTimeout);
        Assert.False(reset.Token.CanBeCanceled);
        rig.AssertUnwound();
    }
}
