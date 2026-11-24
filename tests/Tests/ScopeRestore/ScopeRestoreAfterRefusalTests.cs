using Neemle.OrmLite.Tests.ClosureOperators;
using Xunit;

namespace Neemle.OrmLite.Tests.ScopeRestore;

// ORML-84: a predicate that is refused part-way used to leave the lambda parameters in the render scope, the
// parameters it had already bound in the query's parameter list and the aliases it had taken, so the same Query
// reused after the caught exception ran on a modified state. The refusals used here are the ORML-11 refusal of a
// range over an enum column, which can sit anywhere in a predicate, including inside Db.Exists. A query that went
// through a refusal must render and bind exactly like one that never saw the call, on both providers.
public class ScopeRestoreAfterRefusalTests
{
    private static Query<ClosureItem> Base(string provider) => ClosureItems.Of(provider).Where(n => n.Rank > 1);

    private static Query<ClosureItem> WithExists(Query<ClosureItem> query) =>
        query.Where(n => Db.Exists<ClosureItem>(o => o.Owner == n.Owner));

    // The reused query takes the same successful Exists a fresh query takes, so a leaked alias, parameter or scope
    // entry shows in the text, the bound list or the scope.
    private static void AssertAsIfNeverCalled(Query<ClosureItem> reused, string provider)
    {
        var fresh = WithExists(Base(provider));

        WithExists(reused);

        Assert.Empty(reused.State.Scope);
        Assert.Equal(fresh.RenderSelect(), reused.RenderSelect());
        Assert.Equal(fresh.BoundParameters, reused.BoundParameters);
    }

    [Theory(DisplayName = "AC1 a refusal inside Db.Exists, after a parameter was bound, leaves the query as it was")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_refusal_inside_exists_leaves_the_query_as_it_was(string provider)
    {
        var query = Base(provider);

        Assert.Throws<NotSupportedException>(() => query.Where(
            n => n.Stars == 5 && Db.Exists<ClosureItem>(o => o.Status > FixtureStatus.done)));

        AssertAsIfNeverCalled(query, provider);
    }

    [Theory(DisplayName = "AC1 a refusal two Db.Exists levels deep leaves the query as it was")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_refusal_in_a_nested_exists_leaves_the_query_as_it_was(string provider)
    {
        var query = Base(provider);

        Assert.Throws<NotSupportedException>(() => query.Where(n => Db.Exists<ClosureItem>(
            o => o.Rank == 1 && Db.Exists<ClosureItem>(p => p.Status >= FixtureStatus.done))));

        AssertAsIfNeverCalled(query, provider);
    }

    [Theory(DisplayName = "AC1 a refusal in a plain predicate, after a parameter was bound, leaves the query alone")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_refusal_in_a_predicate_leaves_the_query_as_it_was(string provider)
    {
        var query = Base(provider);

        Assert.Throws<NotSupportedException>(() => query.Where(n => n.Stars == 5 && n.Status < FixtureStatus.done));

        AssertAsIfNeverCalled(query, provider);
    }

    [Theory(DisplayName = "AC1 a refusal inside WhereExists leaves the query as it was")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_refusal_inside_where_exists_leaves_the_query_as_it_was(string provider)
    {
        var query = Base(provider);

        Assert.Throws<NotSupportedException>(
            () => query.WhereExists<ClosureItem>((n, o) => n.Stars == 5 && o.Status > FixtureStatus.done));

        AssertAsIfNeverCalled(query, provider);
    }

    [Theory(DisplayName = "AC2 a predicate that succeeds still leaves no scope entry behind and renders as before")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_successful_predicate_leaves_the_scope_empty(string provider)
    {
        var query = WithExists(Base(provider));

        Assert.Empty(query.State.Scope);
        Assert.Contains("s0.\"owner\" = \"closure_items\".\"owner\"", query.RenderSelect(), StringComparison.Ordinal);
        Assert.Equal(1, Assert.Single(query.BoundParameters));
    }
}
