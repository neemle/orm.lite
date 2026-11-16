using System;
using System.Collections.Generic;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class JsonArrayContainsAnyTests
{
    static JsonArrayContainsAnyTests() =>
        OrmMeta.Register<FixtureDocument>("fixture_documents", Col, ["id", "payload", "label"]);

    private static OrmContext Pg() => new(new NeverUsedDataSource(), new PgDialect());

    private static OrmContext Sqlite() => new(new NeverUsedDataSource(), new SqliteDialect());

    private static Query<FixtureDocument> On(OrmContext ctx) =>
        new(ctx, _ => throw new NotSupportedException("no rows are read here"));

    private static string Col(string member) => member switch
    {
        nameof(FixtureDocument.Id) => "id",
        nameof(FixtureDocument.Payload) => "payload",
        nameof(FixtureDocument.Label) => "label",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown FixtureDocument member."),
    };

    [Fact(DisplayName = "the predicate renders a type-guarded OR of operator-form containment over the extracted key, with the key and every value BOUND")]
    public void The_predicate_renders_the_guarded_any_of_expression()
    {
        IReadOnlyList<string> values = ["urgent", "blocked"];
        var sql = On(Pg()).Where(d => Db.JsonArrayContainsAny(d.Payload, "tags", values)).RenderSelect();

        Assert.Contains(
            "WHERE (jsonb_typeof((\"payload\" -> @p0)) = 'array' AND ("
            + "(\"payload\" -> @p0) @> to_jsonb(CAST(@p1 AS text)) OR (\"payload\" -> @p0) @> to_jsonb(CAST(@p2 AS text))))",
            sql, StringComparison.Ordinal);
        Assert.DoesNotContain("urgent", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("tags", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the predicate is any-of: a single value renders no OR and two values render exactly one")]
    public void The_predicate_joins_values_with_or()
    {
        IReadOnlyList<string> one = ["urgent"];
        IReadOnlyList<string> three = ["a", "b", "c"];

        var single = On(Pg()).Where(d => Db.JsonArrayContainsAny(d.Payload, "tags", one)).RenderSelect();
        var triple = On(Pg()).Where(d => Db.JsonArrayContainsAny(d.Payload, "tags", three)).RenderSelect();

        Assert.DoesNotContain(" OR ", single, StringComparison.Ordinal);
        Assert.Equal(2, triple.Split(" OR ").Length - 1);
    }

    [Fact(DisplayName = "a dialect with no JSON document type refuses the predicate by name, and the dialect member itself refuses rather than falling back")]
    public void A_dialect_without_json_refuses_by_name()
    {
        IReadOnlyList<string> values = ["urgent"];

        var refusal = Assert.Throws<NotSupportedException>(
            () => On(Sqlite()).Where(d => Db.JsonArrayContainsAny(d.Payload, "tags", values)));
        Assert.Contains("SqliteDialect", refusal.Message, StringComparison.Ordinal);

        ISqlDialect sqlite = new SqliteDialect();
        Assert.Contains(
            "jsonb array-membership",
            Assert.Throws<NotSupportedException>(
                () => sqlite.JsonArrayContainsAnyExpression("\"payload\"", "@p0", ["@p1"])).Message,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the predicate refuses a first argument that is not a column, a null key, and a missing, empty or null-bearing value list")]
    public void The_predicate_refuses_unusable_arguments()
    {
        var loose = Jsonb.Parse("[\"urgent\"]");
        string? noKey = null;
        IReadOnlyList<string> some = ["urgent"];
        IReadOnlyList<string>? missing = null;
        IReadOnlyList<string> empty = [];
        IReadOnlyList<string> withNull = ["urgent", null!];

        Assert.Contains(
            "must be a column",
            Assert.Throws<NotSupportedException>(
                () => On(Pg()).Where(d => Db.JsonArrayContainsAny(loose, "tags", some))).Message);
        Assert.Throws<ArgumentNullException>(
            () => On(Pg()).Where(d => Db.JsonArrayContainsAny(d.Payload, noKey!, some)));
        Assert.Throws<ArgumentException>(
            () => On(Pg()).Where(d => Db.JsonArrayContainsAny(d.Payload, "tags", missing!)));
        Assert.Throws<ArgumentException>(
            () => On(Pg()).Where(d => Db.JsonArrayContainsAny(d.Payload, "tags", empty)));
        Assert.Throws<ArgumentException>(
            () => On(Pg()).Where(d => Db.JsonArrayContainsAny(d.Payload, "tags", withNull)));
    }

    [Fact(DisplayName = "the marker is not executable directly")]
    public void The_marker_cannot_be_executed_directly()
    {
        Assert.Throws<InvalidOperationException>(() => Db.JsonArrayContainsAny(null, "tags", ["a"]));
    }
}
