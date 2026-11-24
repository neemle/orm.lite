using Xunit;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class GroupByClauseRefusalTests
{
    private static Query<ClausePlain> Plain(string provider, string clause) =>
        ClauseTargets.Plain.Apply(ClauseTargets.Plain.New(provider).Where(x => x.Name == "n"), clause);

    [Theory(
        DisplayName =
            "[SEC] GroupBy on one key refuses a query that already pages or orders, naming GroupBy and the clause")]
    [MemberData(nameof(ClauseMatrix.Grouped), MemberType = typeof(ClauseMatrix))]
    public void One_key_refuses_the_clause(string provider, string clause)
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => Plain(provider, clause).GroupBy(x => x.Name));

        ClauseNames.Verify(refusal.Message, "GroupBy", clause);
    }

    [Theory(DisplayName = "[SEC] GroupBy on two keys refuses a query that already pages or orders")]
    [MemberData(nameof(ClauseMatrix.Grouped), MemberType = typeof(ClauseMatrix))]
    public void Two_keys_refuse_the_clause(string provider, string clause)
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => Plain(provider, clause).GroupBy(x => x.Name, x => x.Rank));

        ClauseNames.Verify(refusal.Message, "GroupBy", clause);
    }

    [Theory(DisplayName = "[SEC] GroupBy on three keys refuses a query that already pages or orders")]
    [MemberData(nameof(ClauseMatrix.Grouped), MemberType = typeof(ClauseMatrix))]
    public void Three_keys_refuse_the_clause(string provider, string clause)
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => Plain(provider, clause).GroupBy(x => x.Name, x => x.Rank, x => x.Id));

        ClauseNames.Verify(refusal.Message, "GroupBy", clause);
    }

    [Theory(
        DisplayName =
            "[SEC] a clause added to the query AFTER GroupBy is refused at the grouping terminal, in Compile, Render " +
            "and the async form")]
    [MemberData(nameof(ClauseMatrix.Grouped), MemberType = typeof(ClauseMatrix))]
    public async Task A_clause_added_after_GroupBy_is_refused_at_the_terminal(string provider, string clause)
    {
        var query = Plain(provider, "None");
        var grouping = query.GroupBy(x => x.Name);
        grouping.Count();
        ClauseTargets.Plain.Apply(query, clause);

        var compiled = Assert.Throws<InvalidOperationException>(() => grouping.CompileSelect());
        var rendered = Assert.Throws<InvalidOperationException>(() => grouping.RenderSelect());
        var asynchronous = await Assert.ThrowsAsync<InvalidOperationException>(() => grouping.ToListAsync());

        ClauseNames.Verify(compiled.Message, "GroupBy", clause);
        ClauseNames.Verify(rendered.Message, "GroupBy", clause);
        ClauseNames.Verify(asynchronous.Message, "GroupBy", clause);
    }

    [Theory(DisplayName = "the grouping's own Take, Skip, OrderBy and OrderByKey are not refused and still render")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void The_groupings_own_clauses_still_render(string provider)
    {
        var grouping = Plain(provider, "None").GroupBy(x => x.Name);
        var count = grouping.Count();

        var text = grouping.OrderBy(count, descending: true).OrderByKey().Take(5).Skip(10).RenderSelect();

        Assert.Equal(
            "SELECT \"name\", count(*) FROM \"clause_plain\" WHERE \"name\" = @p0 GROUP BY \"name\" "
            + "ORDER BY count(*) DESC NULLS LAST, \"name\" ASC NULLS LAST LIMIT 5 OFFSET 10",
            text);
    }
}
