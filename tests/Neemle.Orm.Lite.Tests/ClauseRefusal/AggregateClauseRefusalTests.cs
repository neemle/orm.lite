using Xunit;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class AggregateClauseRefusalTests
{
    private static Query<ClausePlain> Plain(string provider, string clause) =>
        ClauseTargets.Plain.Apply(ClauseTargets.Plain.New(provider).Where(x => x.Name == "n"), clause);

    [Theory(
        DisplayName =
            "[SEC] CountAsync refuses a paging clause by operation and clause name, and runs no statement")]
    [MemberData(nameof(ClauseMatrix.Paging), MemberType = typeof(ClauseMatrix))]
    public async Task CountAsync_refuses_paging(string provider, string clause)
    {
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => Plain(provider, clause).CountAsync());

        ClauseNames.Verify(refusal.Message, "CountAsync", clause);
    }

    [Theory(
        DisplayName =
            "[SEC] AnyAsync refuses a paging clause and names AnyAsync, never the CountAsync it is built on")]
    [MemberData(nameof(ClauseMatrix.Paging), MemberType = typeof(ClauseMatrix))]
    public async Task AnyAsync_refuses_paging_under_its_own_name(string provider, string clause)
    {
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => Plain(provider, clause).AnyAsync());

        ClauseNames.Verify(refusal.Message, "AnyAsync", clause);
        Assert.DoesNotContain("CountAsync", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "[SEC] SumAsync refuses a paging clause by operation and clause name")]
    [MemberData(nameof(ClauseMatrix.Paging), MemberType = typeof(ClauseMatrix))]
    public async Task SumAsync_refuses_paging(string provider, string clause)
    {
        var query = Plain(provider, clause);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => query.SumAsync(x => x.Rank));

        ClauseNames.Verify(refusal.Message, "SumAsync", clause);
    }

    [Theory(
        DisplayName =
            "[SEC] the aggregate row refuses a paging clause through Compile, Render and the async form, naming " +
            "AggregatesAsync")]
    [MemberData(nameof(ClauseMatrix.Paging), MemberType = typeof(ClauseMatrix))]
    public async Task Aggregates_refuse_paging_in_every_form(string provider, string clause)
    {
        var query = Plain(provider, clause).AggregateCount().AggregateSum(x => x.Rank);

        var compiled = Assert.Throws<InvalidOperationException>(() => query.CompileAggregates());
        var rendered = Assert.Throws<InvalidOperationException>(() => query.RenderAggregates());
        var asynchronous = await Assert.ThrowsAsync<InvalidOperationException>(() => query.AggregatesAsync());

        ClauseNames.Verify(compiled.Message, "AggregatesAsync", clause);
        ClauseNames.Verify(rendered.Message, "AggregatesAsync", clause);
        ClauseNames.Verify(asynchronous.Message, "AggregatesAsync", clause);
    }

    [Theory(
        DisplayName =
            "an ordering clause alone changes neither the statement text nor the refusal of the aggregate row")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void Ordering_alone_leaves_the_aggregate_statement_unchanged(string provider)
    {
        var plain = Plain(provider, "None").AggregateCount().AggregateSum(x => x.Rank).RenderAggregates();
        var ordered = Plain(provider, "OrderBy").AggregateCount().AggregateSum(x => x.Rank).RenderAggregates();
        var descending = Plain(provider, "OrderByDescending").AggregateCount()
            .AggregateSum(x => x.Rank).RenderAggregates();

        Assert.Equal(plain, ordered);
        Assert.Equal(plain, descending);
    }

    [Theory(DisplayName = "a decimal Sum plus a paging clause still reports the decimal refusal first")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public async Task The_decimal_refusal_comes_before_the_clause(string provider)
    {
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Plain(provider, "Take").SumAsync(x => x.Price));

        Assert.Contains("would round a decimal column", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "no aggregate term plus a paging clause still reports the missing aggregate term")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void The_missing_aggregate_term_comes_before_the_clause(string provider)
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => Plain(provider, "Take").CompileAggregates());

        Assert.Contains("at least one aggregate term", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "a clause-free aggregate row renders as before")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void A_clause_free_aggregate_row_renders_unchanged(string provider)
    {
        var text = Plain(provider, "None").AggregateCount().RenderAggregates();

        Assert.Equal("SELECT count(*) FROM \"clause_plain\" WHERE \"name\" = @p0", text);
    }
}
