using System;
using System.Linq.Expressions;
using Neemle.Orm.Lite;
using Neemle.Orm.Lite.Postgres;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class ConflictClauseGuardTests
{
    private static Expression<Func<FixtureWidget, object?>>[] Target(bool any)
    {
        if (!any) return [];
        return [w => w.Id];
    }

    // The set names a column that WidgetRow carries, so resolving the sets passes and the guard under test is reached.
    private static UpsertSet<FixtureWidget>[] Sets(bool any)
    {
        if (!any) return [];
        return [UpsertSet<FixtureWidget>.Inserted(w => w.Id)];
    }

    private static Query<FixtureWidget> On(ISqlProvider provider)
    {
        OrmMeta.Register<FixtureWidget>("fixture_widgets", p => p.ToLowerInvariant(), ["id", "name", "rank"]);
        return new(TestContext.Of(new NeverUsedDataSource(), provider), _ => throw new NotSupportedException());
    }

    private static Row<FixtureWidget> WidgetRow() => new Row<FixtureWidget>().With(w => w.Id, Guid.NewGuid());

    [Theory(DisplayName = "RenderUpsert refuses an undefined OnConflict by name for every target and every set list")]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void An_undefined_action_is_refused_by_name(bool withTarget, bool withSets)
    {
        var refusal = Assert.Throws<ArgumentOutOfRangeException>(
            () => On(PostgresProvider.Instance).RenderUpsert(
                WidgetRow(), Target(withTarget), (OnConflict)99, Sets(withSets)));

        Assert.Equal("action", refusal.ParamName);
        Assert.Contains("OnConflict has no value 99", refusal.Message);
        Assert.Contains("OnConflict.DoNothing", refusal.Message);
        Assert.Contains("OnConflict.DoUpdate", refusal.Message);
    }

    [Theory(DisplayName = "RenderUpsert refuses DoUpdate with no conflict target whether or not any set is given")]
    [InlineData(false)]
    [InlineData(true)]
    public void An_untargeted_do_update_is_refused_by_name(bool withSets)
    {
        var refusal = Assert.Throws<InvalidOperationException>(
            () => On(PostgresProvider.Instance).RenderUpsert(
                WidgetRow(), Target(false), OnConflict.DoUpdate, Sets(withSets)));

        Assert.Contains("conflict target", refusal.Message);
        Assert.Contains("OnConflict.DoNothing", refusal.Message);
    }

    [Fact(DisplayName = "Query refuses an undefined OnConflict before it asks the provider whether it supports upsert, and an untargeted DoUpdate only after")]
    public void The_builder_orders_its_refusals_defined_action_then_upsert_support_then_target()
    {
        var query = On(FixtureProvider.Instance);

        Assert.Throws<ArgumentOutOfRangeException>(() => query.RenderUpsert(WidgetRow(), [], (OnConflict)99));
        Assert.ThrowsAny<NotSupportedException>(() => query.RenderUpsert(WidgetRow(), [], OnConflict.DoUpdate));
    }
}
