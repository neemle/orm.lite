using System;
using Neemle.OrmLite;
using Xunit;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests;

public sealed class MovedFieldNumberOrderRenderingTests
{
    private sealed record FixtureRanked(Guid Id, Jsonb? Doc, DateTime CreatedAt);

    private static string RankedCol(string member) => member switch
    {
        nameof(FixtureRanked.Id) => "id",
        nameof(FixtureRanked.Doc) => "field_document",
        nameof(FixtureRanked.CreatedAt) => "created_at",
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, "Unknown FixtureRanked member."),
    };

    static MovedFieldNumberOrderRenderingTests() =>
        OrmMeta.Register<FixtureRanked>("fixture_ranked", RankedCol, ["id", "field_document", "created_at"]);

    private static Query<FixtureRanked> Ranked(ISqlProvider provider) => new(
        TestContext.Of(new NeverUsedDataSource(), provider),
        _ => throw new NotSupportedException("no rows are read here"));

    private static Query<FixtureRanked> Ordered(ISqlProvider provider, string key = "board_rank") =>
        Ranked(provider).OrderByMovedFieldNumberOrEpochMillis(r => r.Doc, new MovedFieldKey(key), r => r.CreatedAt);

    [Fact(DisplayName = "the board-order term renders the moved key as a guarded double with the creation time in epoch milliseconds as its fallback, ascending, and the later terms follow it")]
    public void The_term_is_a_guarded_double_with_an_epoch_millisecond_fallback()
    {
        var sql = Ordered(PostgresProvider.Instance).ThenBy(r => r.CreatedAt).ThenBy(r => r.Id).RenderSelect();

        Assert.Contains(
            "ORDER BY COALESCE((CASE WHEN pg_input_is_valid((\"field_document\" ->> @p0), 'double precision') "
            + "THEN (\"field_document\" ->> @p0) END)::double precision, "
            + "FLOOR(EXTRACT(EPOCH FROM \"created_at\") * 1000)::double precision) ASC, "
            + "\"created_at\" ASC NULLS LAST, \"id\" ASC NULLS LAST",
            sql,
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "the document key is BOUND, never spliced: its text appears nowhere in the statement")]
    public void The_key_text_is_not_in_the_statement()
    {
        var sql = Ordered(PostgresProvider.Instance).RenderSelect();

        Assert.DoesNotContain("board_rank", sql, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a hostile document key never reaches the statement: the key type refuses it at construction, before any term is built")]
    public void A_hostile_key_is_refused_before_a_term_exists()
    {
        var refusal = Assert.Throws<ArgumentException>(() => Ordered(PostgresProvider.Instance, "x'; DROP TABLE work_items; --"));

        Assert.Contains("rendered into the statement as a SQL string literal", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a dialect with no JSON document type refuses the board-order term by name rather than emitting PostgreSQL text at a server that would not parse it")]
    public void A_dialect_without_json_refuses_by_name()
    {
        var refusal = Assert.ThrowsAny<NotSupportedException>(() => Ordered(FixtureProvider.Instance));

        Assert.Contains("provider 'fixture'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("order by a moved numeric field with an epoch-milliseconds fallback", refusal.Message, StringComparison.Ordinal);
    }
}
