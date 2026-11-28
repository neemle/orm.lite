using Xunit;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ExecutionRig;
using static Neemle.OrmLite.Tests.Maintenance.Execution.ScriptedProvider;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// ADR e8c50bcc v7 D14.1 C2 (D14.5 clause theory): each new effect is admitted immediately before its dispatch. A
// cancel or an expiry is landed in the callback that precedes the effect; the effect is then never dispatched.
public sealed class AdmissionTests
{
    private static readonly ScriptedDbException Busy = new("busy", true);

    // Where each effect's preceding callback is, given the action that lands the cancel or the expiry there.
    private static readonly Dictionary<string, Action<ExecutionRig, Action>> Precedes = new()
    {
        ["gate"] = (rig, land) => rig.Provider.OnRender = land,
        ["open"] = (rig, land) => rig.Provider.Gate = (_, _) =>
        {
            land();
            return ValueTask.FromResult(rig.Provider.Entered());
        },
        ["set"] = (rig, land) => rig.Source.OnOpen = _ =>
        {
            land();
            return Task.CompletedTask;
        },
        ["lookup"] = (rig, land) => rig.Source.Script(Set, _ =>
        {
            land();
            return 0;
        }),
        ["first"] = (rig, land) => rig.Source.OnTimeoutSet = (command, _) =>
        {
            if (command.CommandText == Reindex) land();
        },
        ["retry"] = (rig, land) =>
        {
            rig.Source.Script(Reindex, _ => throw Busy);
            rig.Provider.Classify = _ =>
            {
                land();
                return true;
            };
        },
    };

    // How many times the effect was dispatched that the landing must have stopped.
    private static readonly Dictionary<string, Func<ExecutionRig, int>> Target = new()
    {
        ["gate"] = rig => rig.Log.Count("gate"),
        ["open"] = rig => rig.Log.Count("open"),
        ["set"] = rig => rig.Source.Of(Set).Count,
        ["lookup"] = rig => rig.Source.Of(Lookup).Count,
        ["first"] = rig => rig.Source.Of(Reindex).Count,
        ["retry"] = rig => rig.Source.Of(Reindex).Count - 1,
    };

    [Theory(DisplayName = "ORML-137 D14 C2 admission: a cancel or an expiry in the callback before an effect stops it "
        + "undispatched")]
    [InlineData("gate", true)]
    [InlineData("gate", false)]
    [InlineData("open", true)]
    [InlineData("open", false)]
    [InlineData("set", true)]
    [InlineData("set", false)]
    [InlineData("lookup", true)]
    [InlineData("lookup", false)]
    [InlineData("first", true)]
    [InlineData("first", false)]
    [InlineData("retry", true)]
    [InlineData("retry", false)]
    public async Task C2_Admission(string effect, bool cancel)
    {
        var rig = new ExecutionRig { Provider = { ContentionRetrySlice = TimeSpan.FromSeconds(1) } };
        rig.Provider.Guarded = effect != "retry";
        rig.Provider.LooksUp = effect != "gate";
        Precedes[effect](rig, cancel ? rig.Caller.Cancel : () => rig.Clock.Advance(Ms(1000)));

        var call = rig.ReindexIndexAsync();

        if (cancel) await rig.CancelledAsync(call, effect == "retry" ? Busy : null);
        else Assert.Equal(MaintenanceStatus.Busy, (await call).Status);
        Assert.Equal(0, Target[effect](rig));
        // A statement never dispatched is no event; a retry never dispatched leaves the first attempt's event.
        Assert.Equal(effect == "retry" ? 1 : 0, rig.Sink.Maintenance.Count);
        // The guard's teardown runs exactly when its setup was dispatched.
        Assert.Equal(effect is "lookup" or "first" ? 1 : 0, rig.Source.Of(Reset).Count);
        rig.AssertUnwound();
    }
}
