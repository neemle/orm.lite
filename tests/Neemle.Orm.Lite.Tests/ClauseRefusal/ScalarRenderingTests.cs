using Xunit;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class ScalarRenderingTests
{
    private static Query<ClausePlain> Plain(string provider) =>
        ClauseTargets.Plain.New(provider).Where(x => x.Name == "n");

    [Theory(
        DisplayName =
            "a scalar read without a clause is the statement it always was: the column of the first matching row")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void A_clause_free_scalar_is_unchanged(string provider)
    {
        var query = Plain(provider);

        Assert.Equal(
            "SELECT \"rank\" FROM \"clause_plain\" WHERE \"name\" = @p0 LIMIT 1", query.RenderScalar(x => x.Rank));
        Assert.Equal(query.RenderScalar(x => x.Rank), query.CompileScalar(x => x.Rank).Text);
    }

    [Theory(DisplayName = "a scalar read orders by the Order clause before it takes the first row")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void An_ordered_scalar_takes_the_first_row_of_the_order(string provider)
    {
        var descending = Plain(provider).OrderByDescending(x => x.Rank).RenderScalar(x => x.Name);
        var thenBy = Plain(provider).OrderBy(x => x.Rank).ThenByDescending(x => x.Name).RenderScalar(x => x.Name);

        Assert.Equal(
            "SELECT \"name\" FROM \"clause_plain\" WHERE \"name\" = @p0 "
            + "ORDER BY \"rank\" DESC NULLS LAST LIMIT 1",
            descending);
        Assert.Equal(
            "SELECT \"name\" FROM \"clause_plain\" WHERE \"name\" = @p0 "
            + "ORDER BY \"rank\" ASC NULLS LAST, \"name\" DESC NULLS LAST LIMIT 1",
            thenBy);
    }

    [Theory(DisplayName = "a scalar read honours the offset: the first row AFTER the rows skipped")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void A_scalar_honours_the_offset(string provider)
    {
        var text = Plain(provider).OrderBy(x => x.Rank).Skip(1).RenderScalar(x => x.Name);

        Assert.Equal(
            "SELECT \"name\" FROM \"clause_plain\" WHERE \"name\" = @p0 "
            + "ORDER BY \"rank\" ASC NULLS LAST LIMIT 1 OFFSET 1",
            text);
    }

    [Theory(DisplayName = "a scalar read honours a limit as the smaller of one and the limit, so Take(0) reads no row")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void A_scalar_limit_is_at_most_one(string provider)
    {
        Assert.EndsWith(" LIMIT 0", Plain(provider).Take(0).RenderScalar(x => x.Name), StringComparison.Ordinal);
        Assert.EndsWith(" LIMIT 1", Plain(provider).Take(1).RenderScalar(x => x.Name), StringComparison.Ordinal);
        Assert.EndsWith(" LIMIT 1", Plain(provider).Take(5).RenderScalar(x => x.Name), StringComparison.Ordinal);
        Assert.EndsWith(
            "ORDER BY \"rank\" ASC NULLS LAST LIMIT 1 OFFSET 2",
            Plain(provider).OrderBy(x => x.Rank).Take(9).Skip(2).RenderScalar(x => x.Name),
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a soft-deleted entity keeps its live term in front of the scalar's filter and order")]
    public void A_soft_entity_scalar_keeps_the_live_term()
    {
        var text = ClauseTargets.Soft.New("postgres").Where(x => x.Name == "n")
            .OrderByDescending(x => x.Rank).RenderScalar(x => x.Name);

        Assert.Equal(
            "SELECT \"name\" FROM \"clause_soft\" WHERE \"deleted_at\" IS NULL AND \"name\" = @p0 "
            + "ORDER BY \"rank\" DESC NULLS LAST LIMIT 1",
            text);
    }

    [Fact(
        DisplayName =
            "a row lock on the query is REFUSED by the scalar read, which used to drop it without a word; the same " +
            "query still reads FOR UPDATE through RenderSelect")]
    public void A_scalar_refuses_a_row_lock()
    {
        var locked = Plain("postgres").OrderBy(x => x.Rank).ForUpdate();

        var refusal = Assert.Throws<InvalidOperationException>(() => locked.RenderScalar(x => x.Name));

        LockNames.Verify(refusal.Message, "ScalarAsync");
        Assert.EndsWith("FOR UPDATE", locked.RenderSelect(), StringComparison.Ordinal);
    }
}
