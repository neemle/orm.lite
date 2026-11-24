using System;
using Neemle.OrmLite;
using Xunit;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests;

public class MinAggregateTests
{
    static MinAggregateTests()
    {
        OrmMeta.Register<FixtureWidget>("fixture_widgets", property => property.ToLowerInvariant(), ["id", "name", "rank"]);
    }

    private static Query<FixtureWidget> Widgets(ISqlProvider provider) =>
        new(TestContext.Of(new UnreachableDataSource(), provider), _ => throw new NotSupportedException());

    [Fact(DisplayName = "AggregateMin adds a min() term to the SAME single aggregate statement, after the terms already added, so a text value rides the same scan as the counts")]
    public void AggregateMin_joins_the_one_aggregate_statement()
    {
        var sql = Widgets(PostgresProvider.Instance).Where(w => w.Rank == 1)
            .AggregateCount().AggregateCountDistinct(w => w.Name).AggregateMin(w => w.Name)
            .RenderAggregates();

        Assert.Equal(
            "SELECT count(*), count(DISTINCT \"name\"), min(\"name\") FROM \"fixture_widgets\" WHERE \"rank\" = @p0",
            sql);
        Assert.Equal(1, sql.Split(" FROM ").Length - 1);
    }

    [Fact(DisplayName = "the SQLite test dialect renders the same min() term")]
    public void The_sqlite_dialect_renders_min() =>
        Assert.Equal(
            "SELECT min(\"name\") FROM \"fixture_widgets\"",
            Widgets(FixtureProvider.Instance).AggregateMin(w => w.Name).RenderAggregates());
}
