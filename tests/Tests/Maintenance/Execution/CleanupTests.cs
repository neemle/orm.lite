using Xunit;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ScriptedProvider;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// ADR e8c50bcc v7 D14.1 C6 (D14.5 clause theory): a LIFO cleanup stack runs on every path, each resource once, with
// the guard's teardown at one second, no token and its failure ignored; cleanup never reads or changes the outcome.
public sealed class CleanupTests
{
    private static readonly string[] Tail =
        ["exec " + Reset, "dispose command " + Reset, "dispose command " + Set, "dispose connection", "release gate"];

    [Theory(DisplayName = "ORML-137 D14 C6 cleanup: last first, once each, whatever cancels it; RESET at 1 s, its "
        + "failure ignored")]
    [InlineData("success", "reader")]
    [InlineData("success", "statement")]
    [InlineData("success", "reset")]
    [InlineData("success", "connection")]
    [InlineData("success", "gate")]
    [InlineData("success", "none")]
    [InlineData("busy", "statement")]
    [InlineData("busy", "reset")]
    [InlineData("busy", "connection")]
    [InlineData("busy", "gate")]
    [InlineData("busy", "none")]
    [InlineData("failure", "statement")]
    [InlineData("failure", "reset")]
    [InlineData("failure", "connection")]
    [InlineData("failure", "gate")]
    [InlineData("failure", "none")]
    [InlineData("setfail", "reset")]
    [InlineData("setfail", "connection")]
    [InlineData("setfail", "gate")]
    [InlineData("setfail", "none")]
    [InlineData("setthrow", "gate")]
    [InlineData("setthrow", "none")]
    public async Task C6_Cleanup(string outcome, string at)
    {
        var rig = new ExecutionRig { Provider = { Guarded = true } };
        var failure = new ScriptedDbException(outcome, outcome == "busy");
        var setFails = outcome.StartsWith("set", StringComparison.Ordinal);
        Arrange(rig, outcome, failure);
        CancelDuring(rig, at);

        var call = rig.AnalyzeAsync();

        await AssertOutcome(rig, call, outcome, at, failure);
        string[] expected = outcome switch
        {
            "success" => ["dispose reader", "dispose command " + Analyze, .. Tail],
            "setfail" or "setthrow" => Tail,
            _ => ["dispose command " + Analyze, .. Tail],
        };
        var cleanup = rig.Log.After(setFails ? "exec " + Set : "exec " + Analyze).Where(e => e != "classify");
        Assert.Equal(expected, cleanup);
        var reset = Assert.Single(rig.Source.Of(Reset));
        Assert.Equal(1, reset.CommandTimeout);
        Assert.False(reset.Token.CanBeCanceled);
        rig.AssertUnwound();
    }

    // "setthrow": the setup fails before its task is returned; its teardown was registered before the dispatch.
    private static void Arrange(ExecutionRig rig, string outcome, ScriptedDbException failure)
    {
        if (outcome == "setthrow") rig.Source.Synchronous.Add(Set);
        if (outcome.StartsWith("set", StringComparison.Ordinal)) rig.Source.Script(Set, _ => throw failure);
        rig.Source.Script(Analyze, _ => outcome == "success" ? rig.Rows(["row"]) : throw failure);
    }

    // The caller is cancelled while the named resource is cleaned up; the teardown always fails.
    private static void CancelDuring(ExecutionRig rig, string at)
    {
        rig.Source.Script(Reset, _ =>
        {
            if (at == "reset") rig.Caller.Cancel();
            throw new ScriptedDbException("the teardown failed", false);
        });
        rig.Source.OnDispose = what =>
        {
            if (what == at || what == "command " + Analyze && at == "statement") rig.Caller.Cancel();
            return Task.CompletedTask;
        };
        rig.Provider.OnRelease = () =>
        {
            if (at == "gate") rig.Caller.Cancel();
            return Task.CompletedTask;
        };
    }

    private static async Task AssertOutcome(
        ExecutionRig rig, Task<AnalyzeResult> call, string outcome, string at, Exception failure)
    {
        if (at != "none") await rig.CancelledAsync(call, outcome == "success" ? null : failure);
        else if (outcome == "success") Assert.Equal(MaintenanceStatus.Completed, (await call).Status);
        else if (outcome == "busy") Assert.Equal(MaintenanceStatus.Busy, (await call).Status);
        else Assert.Same(failure, await Record.ExceptionAsync(() => call));
    }
}
