using Xunit;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ExecutionRig;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ScriptedProvider;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// ADR e8c50bcc v7 D14.1 C3 (D14.5 clause theory): an effect's failure is captured, classified once (never when the
// caller already cancelled), and keeps its instance; the caller's token is checked again after the classification.
public sealed class ArbitrationTests
{
    [Theory(DisplayName = "ORML-137 D14 C3 arbitration: a failure is classified once, never once cancelled, and keeps "
        + "its instance")]
    [InlineData("open", "cancels", true)]
    [InlineData("open", "cancels", false)]
    [InlineData("open", "expires", true)]
    [InlineData("open", "expires", false)]
    [InlineData("open", "neither", true)]
    [InlineData("open", "neither", false)]
    [InlineData("open", "already", true)]
    [InlineData("statement", "cancels", true)]
    [InlineData("statement", "cancels", false)]
    [InlineData("statement", "expires", true)]
    [InlineData("statement", "expires", false)]
    [InlineData("statement", "neither", true)]
    [InlineData("statement", "neither", false)]
    [InlineData("statement", "already", true)]
    public async Task C3_Arbitration(string site, string classifier, bool contention)
    {
        var rig = new ExecutionRig();
        var failure = new ScriptedDbException("failed", contention);
        rig.Provider.Classify = classified =>
        {
            if (classifier == "cancels") rig.Caller.Cancel();
            if (classifier == "expires") rig.Clock.Skip(Ms(1000));
            return ((ScriptedDbException)classified).Contention;
        };
        Fail(rig, site, () =>
        {
            if (classifier == "already") rig.Caller.Cancel();
            throw failure;
        });

        var call = rig.AnalyzeAsync();

        if (classifier is "cancels" or "already") await rig.CancelledAsync(call, failure);
        else if (contention) Assert.Equal(MaintenanceStatus.Busy, (await call).Status);
        else Assert.Same(failure, await Record.ExceptionAsync(() => call));
        Assert.Equal(classifier == "already" ? 0 : 1, rig.Provider.Classifications);
        rig.AssertUnwound();
    }

    [Fact(DisplayName = "ORML-137 D14 C3 the open's deadline is Busy with its cancellation as the cause")]
    public async Task The_open_deadline_is_busy()
    {
        var rig = new ExecutionRig();
        rig.Source.OnOpen = token =>
        {
            rig.Clock.Advance(Ms(1000));
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };
        // The cause is kept: a cancel in the last cleanup publishes it inside the cancellation.
        rig.Provider.OnRelease = () => rig.Caller.CancelAsync();

        var failure = await Record.ExceptionAsync(() => rig.AnalyzeAsync());

        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(rig.Caller.Token, cancelled.CancellationToken);
        var deadline = Assert.IsAssignableFrom<OperationCanceledException>(cancelled.InnerException);
        Assert.NotEqual(rig.Caller.Token, deadline.CancellationToken);
        Assert.Equal(0, rig.Provider.Classifications);
        rig.AssertUnwound();
    }

    [Fact(DisplayName = "ORML-137 D14 C3 a cancellation of the open the deadline did not cause propagates unchanged")]
    public async Task An_unrelated_open_cancellation_propagates()
    {
        var rig = new ExecutionRig();
        var own = new OperationCanceledException("the source gave up on its own");
        rig.Source.OnOpen = _ => throw own;

        Assert.Same(own, await Record.ExceptionAsync(() => rig.AnalyzeAsync()));
        Assert.Equal(0, rig.Provider.Classifications);
        rig.AssertUnwound();
    }

    [Fact(DisplayName = "ORML-137 D14 C3 a failure of the diagnostics wrapper before the statement propagates "
        + "unchanged")]
    public async Task A_wrapper_failure_before_dispatch_propagates()
    {
        var rig = new ExecutionRig();
        var refused = new InvalidOperationException("the statement listener refused");
        using var hook = new StatementStartHook();
        StatementStartHook.OnStart(() => throw refused);

        var failure = await Record.ExceptionAsync(() => rig.AnalyzeAsync());

        Assert.Same(refused, failure);
        Assert.Empty(rig.Source.Of(Analyze));
        Assert.Equal(0, rig.Provider.Classifications);
        rig.AssertUnwound();
    }

    private static void Fail(ExecutionRig rig, string site, Action fail)
    {
        if (site == "statement")
            rig.Source.Script(Analyze, _ =>
            {
                fail();
                return null;
            });
        else
            rig.Source.OnOpen = _ =>
            {
                fail();
                return Task.CompletedTask;
            };
    }
}
