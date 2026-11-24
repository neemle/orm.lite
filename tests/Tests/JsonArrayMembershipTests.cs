using System;
using Neemle.OrmLite;
using Xunit;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests;

public sealed class JsonArrayMembershipTests
{
    static JsonArrayMembershipTests()
    {
        OrmMeta.Register<FixtureDocument>("fixture_documents", Col, ["id", "payload", "label"]);
        OrmMeta.Register<FixtureOwner>("fixture_owners", OwnerCol, ["id", "name"]);
    }

    private static OrmContext Pg() => TestContext.Of(new NeverUsedDataSource(), PostgresProvider.Instance);

    private static OrmContext Sqlite() => TestContext.Of(new NeverUsedDataSource(), FixtureProvider.Instance);

    private static Query<FixtureDocument> On(OrmContext ctx) =>
        new(ctx, _ => throw new NotSupportedException("no rows are read here"));

    private static Query<FixtureDocument> Query() => On(Pg());

    private static string Col(string member) => member switch
    {
        nameof(FixtureDocument.Id) => "id",
        nameof(FixtureDocument.Payload) => "payload",
        nameof(FixtureDocument.Label) => "label",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown FixtureDocument member."),
    };

    private sealed record FixtureOwner(Guid Id, string Name);

    private static string OwnerCol(string member) => member switch
    {
        nameof(FixtureOwner.Id) => "id",
        nameof(FixtureOwner.Name) => "name",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown FixtureOwner member."),
    };

    [Fact(DisplayName = "the predicate renders a type-guarded jsonb_exists over the extracted key, with the key and the value BOUND and never spliced into the statement text")]
    public void The_predicate_renders_the_guarded_membership_expression()
    {
        var sql = Query().Where(d => Db.JsonArrayContainsString(d.Payload, "tags", "urgent")).RenderSelect();

        Assert.Contains(
            "WHERE (jsonb_typeof((\"payload\" -> @p0)) = 'array' AND jsonb_exists((\"payload\" -> @p0), @p1))",
            sql, StringComparison.Ordinal);
        Assert.DoesNotContain("tags", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("urgent", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the array-typeof guard is what keeps a scalar or an object at the same key from matching, so the guard's presence in the rendered text is itself part of the contract, not an implementation detail")]
    public void The_typeof_guard_is_present_and_precedes_the_membership_check()
    {
        var sql = Query().Where(d => Db.JsonArrayContainsString(d.Payload, "tags", "urgent")).RenderSelect();

        var typeofIndex = sql.IndexOf("jsonb_typeof", StringComparison.Ordinal);
        var existsIndex = sql.IndexOf("jsonb_exists", StringComparison.Ordinal);
        Assert.True(typeofIndex >= 0 && existsIndex > typeofIndex);
    }

    [Fact(DisplayName = "rendering the same query TWICE binds the key and the value once each, because the term is built where it is added -- the same property the key predicates had to hold for the same reason")]
    public void Rendering_twice_binds_the_key_and_value_once()
    {
        var query = Query().Where(d => Db.JsonArrayContainsString(d.Payload, "tags", "urgent"));

        Assert.Equal(query.RenderSelect(), query.RenderSelect());
        Assert.Contains("@p1", query.RenderSelect(), StringComparison.Ordinal);
        Assert.DoesNotContain("@p2", query.RenderSelect(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a dialect with no JSON document type refuses the predicate BY NAME rather than emitting PostgreSQL operators at a server that would not parse them, the same guard the key predicates use")]
    public void A_dialect_without_json_refuses_by_name()
    {
        var refusal = Assert.ThrowsAny<NotSupportedException>(
            () => On(Sqlite()).Where(d => Db.JsonArrayContainsString(d.Payload, "tags", "urgent")));

        Assert.Contains("provider 'fixture'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("JSON document", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("test array membership inside a JSON document", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the predicate refuses a first argument that is not a column of the entity, the same refusal shape JsonKeyEquals and JsonHasKey use")]
    public void The_predicate_refuses_a_first_argument_that_is_not_a_column()
    {
        var loose = Jsonb.Parse("[\"urgent\"]");

        Assert.Contains(
            "must be a column",
            Assert.Throws<NotSupportedException>(
                () => Query().Where(d => Db.JsonArrayContainsString(loose, "tags", "urgent"))).Message);
    }

    [Fact(DisplayName = "a null key is refused rather than bound as SQL NULL, matching JsonHasKey's refusal for the same reason")]
    public void A_null_key_is_refused()
    {
        string? absent = null;

        Assert.Throws<ArgumentNullException>(
            () => Query().Where(d => Db.JsonArrayContainsString(d.Payload, absent!, "urgent")));
    }

    [Fact(DisplayName = "a null value is refused rather than bound as SQL NULL, because a JSON array-membership test against a NULL argument matches no row at all rather than refusing")]
    public void A_null_value_is_refused()
    {
        string? absent = null;

        var refusal = Assert.Throws<ArgumentNullException>(
            () => Query().Where(d => Db.JsonArrayContainsString(d.Payload, "tags", absent!)));

        Assert.Contains("JSON array-membership test", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the predicate composes inside a correlated EXISTS subquery using the subquery's OWN alias for the extracted column, never the outer table's -- proving the term is alias-aware rather than hardcoded to the top-level table")]
    public void The_predicate_uses_the_correlated_alias_inside_a_nested_exists()
    {
        var sql = new Query<FixtureOwner>(
                Pg(),
                _ => throw new NotSupportedException("no rows are read here"))
            .WhereExists<FixtureDocument>(
                (o, d) => d.Id == o.Id && Db.JsonArrayContainsString(d.Payload, "tags", "urgent"))
            .RenderSelect();

        Assert.Contains(
            "EXISTS (SELECT 1 FROM \"fixture_documents\" s0 WHERE (s0.\"id\" = \"fixture_owners\".\"id\" "
            + "AND (jsonb_typeof((s0.\"payload\" -> @p0)) = 'array' AND jsonb_exists((s0.\"payload\" -> @p0), @p1))))",
            sql, StringComparison.Ordinal);
        Assert.DoesNotContain("tags", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("urgent", sql, StringComparison.Ordinal);
    }
}
