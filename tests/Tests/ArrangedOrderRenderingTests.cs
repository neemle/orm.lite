using System;
using System.Linq;
using Neemle.OrmLite;
using Xunit;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests;

public sealed class ArrangedOrderRenderingTests
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

    private static Query<FixtureOwner> Owners(ISqlProvider provider)
    {
        OrmMeta.Register<FixtureDocument>("fixture_documents", DocumentCol, ["id", "payload", "label"]);
        OrmMeta.Register<FixtureOwner>("fixture_owners", OwnerCol, ["id", "name"]);
        return new Query<FixtureOwner>(
            TestContext.Of(new NeverUsedDataSource(), provider),
            _ => throw new NotSupportedException("no rows are read here"));
    }

    private static Query<FixtureOwner> Owners() => Owners(PostgresProvider.Instance);

    private static string Ordered(params string[] arrangement) => Owners()
        .OrderByJsonKeyOf<FixtureDocument>(
            (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("severity"),
            descending: false, nullsFirst: false, JsonValueOrder.Text, arrangement))
        .RenderSelect();

    [Fact(DisplayName = "an EMPTY arrangement renders the order term EXACTLY as it rendered before this ticket — the nine kinds that carry no option set are untouched, and the expectation is the literal pre-ticket text so a change to both arms is still caught")]
    public void An_empty_arrangement_renders_the_pre_ticket_term()
    {
        var sql = Ordered();

        Assert.Contains(
            "ORDER BY (SELECT (s0.\"payload\" ->> @p0) FROM \"fixture_documents\" s0 "
            + "WHERE s0.\"id\" = \"fixture_owners\".\"id\") ASC NULLS LAST",
            sql,
            StringComparison.Ordinal);
        Assert.DoesNotContain("array_position", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "an arrangement turns the SAME correlated scalar subquery into a RANK over the array position of the extracted value — the subquery envelope, the correlation and the direction clause are unchanged, so no row is joined away and the value-absent rows stay last")]
    public void An_arrangement_ranks_inside_the_same_subquery()
    {
        var sql = Ordered("urgent", "normal", "blocker");

        Assert.Contains(
            "ORDER BY (SELECT array_position(ARRAY[@p1, @p2, @p3]::text[], (s0.\"payload\" ->> @p0)) "
            + "FROM \"fixture_documents\" s0 WHERE s0.\"id\" = \"fixture_owners\".\"id\") ASC NULLS LAST",
            sql,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "every arranged value BINDS to its own placeholder — an option value full of SQL metacharacters never reaches the statement, and the placeholders that replace it are asserted present so the absence cannot pass vacuously")]
    public void Every_arranged_value_binds_and_none_is_spliced()
    {
        const string injected = "'); DROP TABLE work_items; --";
        var sql = Ordered("urgent", injected, "blocker");

        Assert.Contains("array_position(ARRAY[@p1, @p2, @p3]::text[]", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(injected, sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("urgent", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("blocker", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("severity", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "two ordering terms on ONE query keep their OWN arrangements — a single-select ranked by its options followed by an ordinary field compared as text, each binding its own slots, which is why the arrangement is an argument and not a builder setting")]
    public void Two_terms_keep_their_own_arrangements()
    {
        var sql = Owners()
            .OrderByJsonKeyOf<FixtureDocument>(
                (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("severity"),
                descending: false, nullsFirst: false, JsonValueOrder.Text, ["urgent", "normal"]))
            .ThenByJsonKeyOf<FixtureDocument>(
                (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("tier"),
                descending: true, nullsFirst: true, JsonValueOrder.Text, []))
            .RenderSelect();

        Assert.Contains(
            "array_position(ARRAY[@p1, @p2]::text[], (s0.\"payload\" ->> @p0))",
            sql,
            StringComparison.Ordinal);
        Assert.Contains(
            "(SELECT (s1.\"payload\" ->> @p3) FROM \"fixture_documents\" s1 "
            + "WHERE s1.\"id\" = \"fixture_owners\".\"id\") DESC NULLS FIRST",
            sql,
            StringComparison.Ordinal);
        Assert.Equal(1, sql.Split("array_position").Length - 1);
    }

    [Fact(DisplayName = "rendering the same ranked query twice binds the arrangement ONCE — the term is built where it is ADDED, the same property the key predicates and the cross-table JSON order had to hold for the same reason")]
    public void Rendering_twice_binds_the_arrangement_once()
    {
        var query = Owners()
            .OrderByJsonKeyOf<FixtureDocument>(
                (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("severity"),
                descending: false, nullsFirst: false, JsonValueOrder.Text, ["urgent", "normal"]))
            .ThenBy(o => o.Id);

        Assert.Equal(query.RenderSelect(), query.RenderSelect());
        Assert.DoesNotContain("@p3", query.RenderSelect(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a NULL inside the arrangement is refused BY NAME rather than bound, because array_position matches NULL against the value-absent rows and would rank them among the real options with nothing erroring")]
    public void A_null_arranged_value_is_refused_by_name()
    {
        var refusal = Assert.Throws<ArgumentNullException>(
            () => Ordered("urgent", null!, "blocker"));

        Assert.Contains("arranged option value must be a string", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("value-absent row", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a dialect with no JSON document type REFUSES the ranked ordering BY NAME, before any arrangement is bound, rather than emitting PostgreSQL text at a server that would not parse it")]
    public void A_dialect_without_json_refuses_by_name()
    {
        var refusal = Assert.ThrowsAny<NotSupportedException>(() => Owners(FixtureProvider.Instance)
            .OrderByJsonKeyOf<FixtureDocument>(
                (o, d) => d.Id == o.Id, d => d.Payload, new JsonKeyOrdering(new JsonKey("severity"),
                descending: false, nullsFirst: false, JsonValueOrder.Text, ["urgent", "normal"])));

        Assert.Contains("provider 'fixture'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("order by a key inside a JSON document in another table",
            refusal.Message, StringComparison.Ordinal);
    }
}
