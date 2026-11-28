using Xunit;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ExecutionRig;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ScriptedProvider;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// ADR e8c50bcc v7 D14.1 C8 (D14.5 clause theory): one injected clock and one latched deadline. Admission reads the
// clock; the timer, armed at the remainder rounded up, re-reads it and only then cancels the open's token. Only the
// open gets the deadline's token: the gate gets the caller's token and the admitted remainder as its timeout.
public sealed class DeadlineTests
{
    private static readonly Dictionary<string, Scenario> Scenarios = new()
    {
        // The gate waits its whole timeout with the caller live: a cause-less Busy, never a cancellation, and no
        // deadline token ever reaches it (the attack F1 on the design).
        ["gate-held"] = new(Ms(1000), MaintenanceStatus.Busy, (rig, _) => rig.Provider.Gate = (timeout, _) =>
        {
            rig.Clock.Advance(timeout + Ms(1));
            return ValueTask.FromResult(new MaintenanceWriteLease(false, null));
        }, rig =>
        {
            Assert.Equal(rig.Caller.Token, Assert.Single(rig.Provider.GateTokens));
            Assert.Empty(rig.Clock.Timers);
        }),
        ["minus-one-tick"] = new(Ms(1000), MaintenanceStatus.Completed,
            (rig, total) => OnOpen(rig, () => rig.Clock.Skip(total - TimeSpan.FromTicks(1))),
            rig => Assert.Equal(1, Assert.Single(rig.Source.Of(Analyze)).CommandTimeout)),
        ["exact"] = new(Ms(1000), MaintenanceStatus.Busy,
            (rig, total) => OnOpen(rig, () => rig.Clock.Skip(total)), NothingDispatched),
        ["beyond"] = new(Ms(1000), MaintenanceStatus.Busy,
            (rig, total) => OnOpen(rig, () => rig.Clock.Skip(total + TimeSpan.FromTicks(1))), NothingDispatched),
        ["timer-early"] = new(Ms(1000), MaintenanceStatus.Completed,
            (rig, _) => OnOpen(rig, () => FireAfter(rig, Ms(500))),
            rig => Assert.Equal([Ms(1000), Ms(500)], rig.Clock.Timer.Arms)),
        // The timer comes after the root disposed it: it latches nothing, cancels nothing, throws nothing.
        ["timer-late"] = new(Ms(1000), MaintenanceStatus.Busy,
            (rig, total) => OnOpen(rig, () => rig.Clock.Skip(total)), rig =>
            {
                Assert.True(rig.Clock.Timer.Disposed);
                rig.Clock.Timer.Fire();
                NothingDispatched(rig);
            }),
        ["timer-before-completion"] = new(Ms(1000), MaintenanceStatus.Busy, (rig, total) => rig.Source.OnOpen = token =>
        {
            rig.Clock.Advance(total);
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }, NothingDispatched),
        ["timer-after-completion"] = new(Ms(1000), MaintenanceStatus.Completed, (rig, total) =>
            rig.Source.Script(Analyze, _ =>
            {
                rig.Clock.Advance(total);
                return rig.Rows(["row"]);
            }), _ => { }),
        ["fractional"] = new(TimeSpan.FromTicks(4_003_000), MaintenanceStatus.Completed,
            (rig, _) => OnOpen(rig, () => FireAfter(rig, Ms(400))),
            rig => Assert.Equal([Ms(401), Ms(1)], rig.Clock.Timer.Arms)),
        ["clamp"] = new(TimeSpan.FromDays(30), MaintenanceStatus.Completed,
            (rig, _) => OnOpen(rig, () => rig.Clock.Advance(Ms(int.MaxValue))),
            rig => Assert.Equal([Ms(int.MaxValue), Rest(TimeSpan.FromDays(30))], rig.Clock.Timer.Arms)),
        ["sqlite-slice"] = new(Ms(2300), MaintenanceStatus.Completed,
            (rig, _) => rig.Provider.ContentionRetrySlice = TimeSpan.FromSeconds(1),
            rig => Assert.Equal(1, Assert.Single(rig.Source.Of(Analyze)).CommandTimeout)),
    };

    [Theory(DisplayName = "ORML-137 D14 C8 deadline: one clock, a latched expiry, a timer that re-reads it; only the "
        + "open gets the deadline")]
    [InlineData("gate-held")]
    [InlineData("minus-one-tick")]
    [InlineData("exact")]
    [InlineData("beyond")]
    [InlineData("timer-early")]
    [InlineData("timer-late")]
    [InlineData("timer-before-completion")]
    [InlineData("timer-after-completion")]
    [InlineData("fractional")]
    [InlineData("clamp")]
    [InlineData("sqlite-slice")]
    public async Task C8_Deadline(string name)
    {
        var rig = new ExecutionRig();
        var scenario = Scenarios[name];
        scenario.Arrange(rig, scenario.Total);
        var request = new AnalyzeRequest(AnalyzeScope.Database) { Timeout = scenario.Total };

        var result = await rig.Maintenance.AnalyzeAsync(request, rig.Caller.Token);

        Assert.Equal(scenario.Expected, result.Status);
        scenario.Verify(rig);
        rig.AssertUnwound();
    }

    [Fact(DisplayName = "ORML-137 D14 C8 PostgreSQL's guard: the setup gets the remainder, the lookup and statement "
        + "one second of grace")]
    public async Task The_guard_grace()
    {
        var rig = new ExecutionRig { Provider = { Guarded = true, LooksUp = true } };

        Assert.Equal(MaintenanceStatus.Completed, (await rig.ReindexIndexAsync(2300)).Status);

        var timeouts = new[] { Set, Lookup, Reindex }.Select(t => Assert.Single(rig.Source.Of(t)).CommandTimeout);
        Assert.Equal([3, 4, 4], timeouts);
        rig.AssertUnwound();
    }

    private static void OnOpen(ExecutionRig rig, Action action) => rig.Source.OnOpen = _ =>
    {
        action();
        return Task.CompletedTask;
    };

    // Moves the clock without firing, then fires the timer early.
    private static void FireAfter(ExecutionRig rig, TimeSpan elapsed)
    {
        rig.Clock.Skip(elapsed);
        rig.Clock.Timer.Fire();
    }

    private static void NothingDispatched(ExecutionRig rig) => Assert.Empty(rig.Source.Of(Analyze));

    private static TimeSpan Rest(TimeSpan total) => Ms(Math.Ceiling((total - Ms(int.MaxValue)).TotalMilliseconds));

    private sealed record Scenario(
        TimeSpan Total,
        MaintenanceStatus Expected,
        Action<ExecutionRig, TimeSpan> Arrange,
        Action<ExecutionRig> Verify);
}
