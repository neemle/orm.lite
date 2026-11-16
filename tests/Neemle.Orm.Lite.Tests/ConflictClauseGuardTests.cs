using System;
using System.Collections.Generic;
using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class ConflictClauseGuardTests
{
    private static readonly PgDialect Pg = new();

    public static IEnumerable<object[]> UnchangedRenders() =>
    [
        [new string[0], OnConflict.DoNothing, new UpsertSet[0], " ON CONFLICT DO NOTHING"],
        [new[] { "id" }, OnConflict.DoNothing, new UpsertSet[0], " ON CONFLICT (\"id\") DO NOTHING"],
        [new[] { "id" }, OnConflict.DoNothing, new[] { UpsertSet.ToParam("rank", 0) }, " ON CONFLICT (\"id\") DO NOTHING"],
        [new[] { "id" }, OnConflict.DoUpdate, new UpsertSet[0], " ON CONFLICT (\"id\") DO NOTHING"],
        [new[] { "id" }, OnConflict.DoUpdate, new[] { UpsertSet.ToParam("rank", 0) }, " ON CONFLICT (\"id\") DO UPDATE SET \"rank\" = @p0"],
        [new[] { "id" }, OnConflict.DoUpdate, new[] { UpsertSet.Increment("rank", 1) }, " ON CONFLICT (\"id\") DO UPDATE SET \"rank\" = \"t\".\"rank\" + 1"],
        [new[] { "id" }, OnConflict.DoUpdate, new[] { UpsertSet.Now("updated") }, " ON CONFLICT (\"id\") DO UPDATE SET \"updated\" = now()"],
        [new[] { "id" }, OnConflict.DoUpdate, new[] { UpsertSet.ToJsonDocumentParam("doc", 0) }, " ON CONFLICT (\"id\") DO UPDATE SET \"doc\" = @p0::jsonb"],
    ];

    [Theory(DisplayName = "PgDialect.ConflictClause renders every untargeted DoNothing and every targeted form exactly as it did before the guard")]
    [MemberData(nameof(UnchangedRenders))]
    public void The_unchanged_forms_render_byte_for_byte(
        string[] conflictColumns, OnConflict action, UpsertSet[] sets, string expected) =>
        Assert.Equal(expected, Pg.ConflictClause(conflictColumns, action, sets, "t"));

    private static UpsertSet[] Sets(bool any) => any ? [UpsertSet.ToParam("rank", 0)] : [];

    [Theory(DisplayName = "PgDialect.ConflictClause called directly refuses an undefined OnConflict by name for every target and every set list")]
    [InlineData(new string[0], false)]
    [InlineData(new string[0], true)]
    [InlineData(new[] { "id" }, false)]
    [InlineData(new[] { "id" }, true)]
    public void An_undefined_action_is_refused_by_name(string[] conflictColumns, bool withSets)
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => Pg.ConflictClause(conflictColumns, (OnConflict)99, Sets(withSets), "t"));

        Assert.Equal("action", refusal.ParamName);
        Assert.Contains("OnConflict has no value 99", refusal.Message);
        Assert.Contains("OnConflict.DoNothing", refusal.Message);
        Assert.Contains("OnConflict.DoUpdate", refusal.Message);
    }

    [Theory(DisplayName = "PgDialect.ConflictClause called directly refuses DoUpdate with no conflict target whether or not any set is given")]
    [InlineData(false)]
    [InlineData(true)]
    public void An_untargeted_do_update_is_refused_by_name(bool withSets)
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => Pg.ConflictClause([], OnConflict.DoUpdate, Sets(withSets), "t"));

        Assert.Contains("conflict target", refusal.Message);
        Assert.Contains("OnConflict.DoNothing", refusal.Message);
    }

    private static Query<FixtureWidget> NonUpsertQuery()
    {
        OrmMeta.Register<FixtureWidget>("fixture_widgets", p => p.ToLowerInvariant(), ["id", "name", "rank"]);
        return new(new OrmContext(new NeverUsedDataSource(), new SqliteDialect()), _ => throw new NotSupportedException());
    }

    private static Row<FixtureWidget> WidgetRow() => new Row<FixtureWidget>().With(w => w.Id, Guid.NewGuid());

    [Fact(DisplayName = "Query refuses an undefined OnConflict before it asks the dialect whether it supports upsert, and an untargeted DoUpdate only after")]
    public void The_builder_orders_its_refusals_defined_action_then_upsert_support_then_target()
    {
        var query = NonUpsertQuery();

        Assert.Throws<ArgumentOutOfRangeException>(() => query.RenderUpsert(WidgetRow(), [], (OnConflict)99));
        Assert.Throws<NotSupportedException>(() => query.RenderUpsert(WidgetRow(), [], OnConflict.DoUpdate));
    }

    [Fact(DisplayName = "PgDialect.ConflictClause keeps the target predicate between the conflict columns and the action")]
    public void The_target_predicate_form_is_unchanged() =>
        Assert.Equal(
            " ON CONFLICT (\"scope\") WHERE \"deleted_at\" IS NULL DO UPDATE SET \"rank\" = @p0",
            Pg.ConflictClause(["scope"], OnConflict.DoUpdate, [UpsertSet.ToParam("rank", 0)], "t", "\"deleted_at\" IS NULL"));
}
