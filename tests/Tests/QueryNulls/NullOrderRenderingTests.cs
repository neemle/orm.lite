using Neemle.OrmLite.Tests.ClosureOperators;
using Xunit;

namespace Neemle.OrmLite.Tests.QueryNulls;

// ORML-12 (B11): every term a plain order adds states NULLS LAST, on both providers, so the same query reads the same
// page whichever database answers it.
public class NullOrderRenderingTests
{
    [Theory(DisplayName = "ORML-12 OrderBy, ThenBy and their descending forms render NULLS LAST on both providers")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void Every_plain_order_term_states_nulls_last(string provider)
    {
        var text = ClosureItems.Of(provider).OrderBy(n => n.Level).ThenByDescending(n => n.Title)
            .ThenBy(n => n.Rank).RenderSelect();

        Assert.EndsWith(
            "ORDER BY \"level\" ASC NULLS LAST, \"title\" DESC NULLS LAST, \"rank\" ASC NULLS LAST", text);
    }

    [Theory(DisplayName = "ORML-12 the predicate orders and the descending form render NULLS LAST on both providers")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void The_predicate_orders_state_nulls_last(string provider)
    {
        var first = ClosureItems.Of(provider).OrderByPredicate(n => n.Rank > 1).RenderSelect();
        var then = ClosureItems.Of(provider).OrderByDescending(n => n.Level).ThenByPredicate(n => n.Rank == 2)
            .RenderSelect();

        Assert.EndsWith("ORDER BY (\"rank\" > @p0) ASC NULLS LAST", first);
        Assert.EndsWith("ORDER BY \"level\" DESC NULLS LAST, (\"rank\" = @p0) ASC NULLS LAST", then);
    }
}
