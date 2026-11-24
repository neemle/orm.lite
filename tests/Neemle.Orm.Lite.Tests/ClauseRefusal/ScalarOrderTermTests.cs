using Xunit;

namespace Neemle.Orm.Lite.Tests.ClauseRefusal;

public sealed class ScalarOrderTermTests
{
    private static Query<ClausePlain> Plain(string provider) => ClauseTargets.Plain.New(provider);

    private static IEnumerable<object?> Values(Statement statement) => statement.Parameters.Select(p => p.Value);

    [Theory(
        DisplayName =
            "an order term that binds a parameter numbers it in the order the calls were made, and the scalar binds " +
            "the same values as the row read")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void A_predicate_order_term_binds_in_call_order(string provider)
    {
        var scalar = Plain(provider).OrderByPredicate(x => x.Rank == 3).Where(x => x.Name == "n");
        var rows = Plain(provider).OrderByPredicate(x => x.Rank == 3).Where(x => x.Name == "n").Take(1);

        Assert.Equal(
            "SELECT \"name\" FROM \"clause_plain\" WHERE \"name\" = @p1 "
            + "ORDER BY (\"rank\" = @p0) ASC NULLS LAST LIMIT 1",
            scalar.RenderScalar(x => x.Name));
        Assert.Equal(Values(rows.CompileProject([x => x.Name])), Values(scalar.CompileScalar(x => x.Name)));
        Assert.Equal<object?>([3, "n"], Values(scalar.CompileScalar(x => x.Name)));
    }

    [Theory(DisplayName = "a JSON-key order term is a bound key in the scalar read exactly as in the row read")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void A_json_key_order_term_binds_its_key(string provider)
    {
        var scalar = Plain(provider).Where(x => x.Name == "n")
            .OrderByJsonKey(x => x.Doc, new JsonKey("tier"), descending: true, nullsFirst: false);
        var rows = Plain(provider).Where(x => x.Name == "n")
            .OrderByJsonKey(x => x.Doc, new JsonKey("tier"), descending: true, nullsFirst: false).Take(1);

        var expected = rows.RenderProject([x => x.Name]);

        Assert.Equal(expected, scalar.RenderScalar(x => x.Name));
        Assert.Equal(Values(rows.CompileProject([x => x.Name])), Values(scalar.CompileScalar(x => x.Name)));
        Assert.Contains("ORDER BY", expected, StringComparison.Ordinal);
    }

    [Theory(
        DisplayName =
            "the scalar read is the row read with the limit forced to one: same order, same offset, same parameters")]
    [MemberData(nameof(ClauseMatrix.EveryProvider), MemberType = typeof(ClauseMatrix))]
    public void The_scalar_is_the_first_row_of_the_row_read(string provider)
    {
        var scalar = Plain(provider).Where(x => x.Name == "n").OrderBy(x => x.Rank).ThenBy(x => x.Price).Skip(3);
        var rows = Plain(provider).Where(x => x.Name == "n").OrderBy(x => x.Rank).ThenBy(x => x.Price).Skip(3).Take(1);

        Assert.Equal(rows.RenderProject([x => x.Name]), scalar.RenderScalar(x => x.Name));
    }
}
