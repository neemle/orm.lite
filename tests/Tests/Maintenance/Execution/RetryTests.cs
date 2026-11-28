using Xunit;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ExecutionRig;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ScriptedProvider;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// ADR e8c50bcc v7 D14.1 C4 (D14.5 clause theory): one retry loop inside the executor, around statement attempts only,
// timed by one diagnostics event; every attempt is admitted, and the pending contention clears only on success.
public sealed class RetryTests
{
    [Theory(DisplayName = "ORML-137 D14 C4 retry: one loop, one event, slice timeouts; the cause clears on success "
        + "and survives a stop")]
    [InlineData("success")]
    [InlineData("cancel")]
    [InlineData("expire")]
    public async Task C4_Retry(string ending)
    {
        var rig = new ExecutionRig { Provider = { ContentionRetrySlice = TimeSpan.FromSeconds(2) } };
        ScriptedDbException[] busy = [new("busy 1", true), new("busy 2", true)];
        rig.Source.Script(Analyze, dispatch =>
        {
            rig.Clock.Advance(ending == "expire" ? Ms(5000) : Ms(2000));
            if (dispatch.Attempt <= 2) throw busy[dispatch.Attempt - 1];
            // After the success, a cancel while reading: the published cancellation carries no stale contention.
            return new ScriptedReader(rig.Source, [["row"]]) { OnRead = (_, _) => rig.Caller.CancelAsync() };
        });
        // "cancel": the cancel lands in the second attempt's timeout setter, after the first attempt's contention.
        if (ending == "cancel")
            rig.Source.OnTimeoutSet = (_, _) =>
            {
                if (rig.Source.Of(Analyze).Count == 1) rig.Caller.Cancel();
            };

        var call = rig.AnalyzeAsync(5000);

        if (ending == "expire") Assert.Equal(MaintenanceStatus.Busy, (await call).Status);
        else await rig.CancelledAsync(call, ending == "success" ? null : busy[0]);
        // One event for every attempt; a stop after a dispatch reports the native failure.
        var statement = Assert.Single(rig.Sink.Maintenance);
        int[] timeouts = ending == "success" ? [2, 2, 1] : [2];
        Assert.Equal(timeouts, rig.Source.Of(Analyze).Select(d => d.CommandTimeout));
        Assert.Same(ending == "success" ? null : busy[0], statement.Error);
        Assert.Single(rig.Source.Connections);
        rig.AssertUnwound();
    }

    [Fact(DisplayName = "ORML-137 D14 C4 a cancel inside a retried attempt publishes the earlier contention as the "
        + "cause")]
    public async Task A_cancel_inside_a_retry_keeps_the_contention()
    {
        var rig = new ExecutionRig { Provider = { ContentionRetrySlice = TimeSpan.FromSeconds(1) } };
        var busy = new ScriptedDbException("busy", true);
        rig.Source.Script(Analyze, dispatch =>
        {
            if (dispatch.Attempt == 1) throw busy;
            rig.Caller.Cancel();
            throw new OperationCanceledException(dispatch.Token);
        });

        await rig.CancelledAsync(rig.AnalyzeAsync(5000), busy);

        Assert.Equal(2, rig.Source.Of(Analyze).Count);
        Assert.Equal(1, rig.Provider.Classifications);
        rig.AssertUnwound();
    }
}
