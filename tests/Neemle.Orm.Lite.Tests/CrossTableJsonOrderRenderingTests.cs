using System;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class CrossTableJsonOrderRenderingTests
{
    private sealed record FixtureOwner(Guid Id, string Name);

    private static string OwnerCol(string member) => member switch
    {
        nameof(FixtureOwner.Id) => "id",
        nameof(FixtureOwner.Name) => "name",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown FixtureOwner member."),
    };

    private static string DocumentCol(string member) => member switch
    {
        nameof(FixtureDocument.Id) => "id",
        nameof(FixtureDocument.Payload) => "payload",
        nameof(FixtureDocument.Label) => "label",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown FixtureDocument member."),
    };

    private static Query<FixtureOwner> Owners(ISqlDialect dialect)
    {
        OrmMeta.Register<FixtureDocument>("fixture_documents", DocumentCol, ["id", "payload", "label"]);
        OrmMeta.Register<FixtureOwner>("fixture_owners", OwnerCol, ["id", "name"]);
        return new Query<FixtureOwner>(
            TestContext.Of(new NeverUsedDataSource(), dialect),
            _ => throw new NotSupportedException("no rows are read here"));
    }

    private static Query<FixtureOwner> Owners() => Owners(new PgDialect());

    [Fact(DisplayName = "the cross-table order term is a correlated SCALAR SUBQUERY over the other table's document, correlated by the lambda, with the key BOUND and never spliced")]
    public void The_order_term_is_a_correlated_scalar_subquery()
    {
        var sql = Owners()
            .OrderByJsonKeyOf<FixtureDocument>(
                (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("tier"), descending: false, nullsFirst: false, JsonValueOrder.Text, []))
            .RenderSelect();

        Assert.Contains(
            "ORDER BY (SELECT (s0.\"payload\" ->> @p0) FROM \"fixture_documents\" s0 "
            + "WHERE s0.\"id\" = \"fixture_owners\".\"id\") ASC NULLS LAST",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("tier", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the direction and the nulls position are both stated, and the nulls position does not move with the direction — the four combinations render four different terms")]
    public void The_four_combinations_render_four_different_terms()
    {
        Assert.Contains(") ASC NULLS LAST", Term(descending: false, nullsFirst: false), StringComparison.Ordinal);
        Assert.Contains(") ASC NULLS FIRST", Term(descending: false, nullsFirst: true), StringComparison.Ordinal);
        Assert.Contains(") DESC NULLS LAST", Term(descending: true, nullsFirst: false), StringComparison.Ordinal);
        Assert.Contains(") DESC NULLS FIRST", Term(descending: true, nullsFirst: true), StringComparison.Ordinal);

        static string Term(bool descending, bool nullsFirst) => Owners()
            .OrderByJsonKeyOf<FixtureDocument>(
                (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("tier"), descending, nullsFirst, JsonValueOrder.Text, []))
            .RenderSelect();
    }

    [Fact(DisplayName = "ThenByJsonKeyOf APPENDS while OrderByJsonKeyOf REPLACES, and each term binds its own key — so a second ordering cannot silently drop the first")]
    public void ThenBy_appends_and_OrderBy_replaces()
    {
        var appended = Owners()
            .OrderByJsonKeyOf<FixtureDocument>(
                (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("tier"), descending: false, nullsFirst: false, JsonValueOrder.Text, []))
            .ThenByJsonKeyOf<FixtureDocument>(
                (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("rank"), descending: true, nullsFirst: true, JsonValueOrder.Text, []))
            .RenderSelect();

        Assert.Contains("@p0", appended, StringComparison.Ordinal);
        Assert.Contains("@p1", appended, StringComparison.Ordinal);
        Assert.Contains("ASC NULLS LAST, (SELECT", appended, StringComparison.Ordinal);
        Assert.Contains("DESC NULLS FIRST", appended, StringComparison.Ordinal);

        var replaced = Owners()
            .OrderByJsonKeyOf<FixtureDocument>(
                (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("tier"), descending: false, nullsFirst: false, JsonValueOrder.Text, []))
            .OrderByJsonKeyOf<FixtureDocument>(
                (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("rank"), descending: true, nullsFirst: true, JsonValueOrder.Text, []))
            .RenderSelect();

        Assert.DoesNotContain("ASC NULLS LAST", replaced, StringComparison.Ordinal);
        Assert.Contains("DESC NULLS FIRST", replaced, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the term is built where it is ADDED, so rendering the same query twice binds the key once — the same property the key predicates had to hold for the same reason")]
    public void Rendering_twice_binds_the_key_once()
    {
        var query = Owners()
            .OrderByJsonKeyOf<FixtureDocument>(
                (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("tier"), descending: false, nullsFirst: false, JsonValueOrder.Text, []))
            .ThenBy(o => o.Id);

        Assert.Equal(query.RenderSelect(), query.RenderSelect());
        Assert.DoesNotContain("@p1", query.RenderSelect(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a dialect with no JSON document type REFUSES this ordering BY NAME rather than emitting PostgreSQL operators at a server that would not parse them")]
    public void A_dialect_without_json_refuses_by_name()
    {
        var refusal = Assert.Throws<NotSupportedException>(() => Owners(new SqliteDialect())
            .OrderByJsonKeyOf<FixtureDocument>(
                (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("tier"), descending: false, nullsFirst: false, JsonValueOrder.Text, [])));

        Assert.Contains("SqliteDialect", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("order by a key inside a JSON document in another table",
            refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the direction clause is the DIALECT's, and its own member takes no string at all — two booleans in, one clause out, so there is no text-shaped door into it")]
    public void The_direction_clause_belongs_to_the_dialect_and_takes_no_string()
    {
        ISqlDialect pg = new PgDialect();

        Assert.Equal(" ASC NULLS LAST", pg.OrderDirectionClause(descending: false, nullsFirst: false));
        Assert.Equal(" DESC NULLS FIRST", pg.OrderDirectionClause(descending: true, nullsFirst: true));

        Assert.Equal(
            pg.JsonKeyTextExpression("\"payload\"", "@p0") + pg.OrderDirectionClause(true, false),
            pg.JsonKeyOrderTerm("\"payload\"", "@p0", descending: true, nullsFirst: false));
    }
}
