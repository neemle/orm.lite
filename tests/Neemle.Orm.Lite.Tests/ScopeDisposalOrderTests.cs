using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class ScopeDisposalOrderTests
{
    private static string? Stamp() => DbActor.Current?.Stamp;

    private static string Id(Guid id) => id.ToString("D");

    [Fact(DisplayName = "disposing an earlier actor scope first keeps the later one in force and never brings the "
        + "disposed one back")]
    public void An_earlier_actor_scope_disposed_first_keeps_the_later_one()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var first = DbActor.EnterUser(a);
        var second = DbActor.EnterUser(b);

        first.Dispose();
        Assert.Equal(Id(b), Stamp());

        second.Dispose();
        Assert.Null(Stamp());
    }

    [Fact(DisplayName = "three actor scopes disposed middle, first, last leave the actor in force at each step "
        + "and none at the end")]
    public void Actor_scopes_disposed_in_any_order_follow_the_survivors()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var scopes = ids.Select(DbActor.EnterUser).ToArray();

        scopes[1].Dispose();
        Assert.Equal(Id(ids[2]), Stamp());
        scopes[0].Dispose();
        Assert.Equal(Id(ids[2]), Stamp());
        scopes[2].Dispose();
        Assert.Null(Stamp());
    }

    [Fact(DisplayName = "disposing an actor scope twice changes nothing the second time")]
    public void An_actor_scope_disposes_once()
    {
        var a = Guid.NewGuid();
        var outer = DbActor.EnterUser(a);
        var inner = DbActor.EnterUser(Guid.NewGuid());

        inner.Dispose();
        inner.Dispose();

        Assert.Equal(Id(a), Stamp());
        outer.Dispose();
        Assert.Null(Stamp());
    }

    [Fact(DisplayName = "disposing an earlier lane scope first keeps the later lane in force, "
        + "then falls back to Background")]
    public void An_earlier_lane_scope_disposed_first_keeps_the_later_one()
    {
        var first = DbLane.Enter(DbLaneKind.Request);
        var second = DbLane.Enter(DbLaneKind.Ops);

        first.Dispose();
        Assert.Equal(DbLaneKind.Ops, DbLane.Current);
        Assert.Same(second, DbLane.CurrentScope);

        second.Dispose();
        Assert.Equal(DbLaneKind.Background, DbLane.Current);
        Assert.Null(DbLane.CurrentScope);
    }

    [Fact(DisplayName = "lane scopes disposed twice, or inner before outer, restore the outer lane")]
    public void Lane_scopes_restore_the_outer_lane()
    {
        var outer = DbLane.Enter(DbLaneKind.Request);
        var inner = DbLane.Enter(DbLaneKind.Ops);

        inner.Dispose();
        inner.Dispose();

        Assert.Equal(DbLaneKind.Request, DbLane.Current);
        outer.Dispose();
        Assert.Equal(DbLaneKind.Background, DbLane.Current);
    }
}
