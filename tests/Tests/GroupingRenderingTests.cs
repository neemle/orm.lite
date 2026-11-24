using System;
using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests;

public class GroupingRenderingTests
{
    static GroupingRenderingTests()
    {
        OrmMeta.Register<FixtureWidget>("fixture_widgets", property => property.ToLowerInvariant(), ["id", "name", "rank"]);
    }

    private static Query<FixtureWidget> Widgets(ISqlProvider provider) =>
        new(new OrmContext(new UnreachableDataSource(), provider), _ => throw new NotSupportedException());

    public static TheoryData<string> Providers => ["postgres", "sqlite"];

    private static ISqlProvider Provider(string id) => id == "postgres" ? PostgresProvider.Instance : SqliteProvider.Instance;

    [Theory(DisplayName = "a grouped query writes the key first, then the aggregates in the order they were added, then WHERE, GROUP BY, HAVING, ORDER BY and paging, the same on both providers")]
    [MemberData(nameof(Providers))]
    public void A_grouped_query_has_the_standard_clause_order(string id)
    {
        var grouping = Widgets(Provider(id)).Where(w => w.Rank > 0).GroupBy(w => w.Name);
        var count = grouping.Count();
        var sum = grouping.Sum<long>(w => w.Rank);
        grouping.Min<int?>(w => w.Rank);
        grouping.Max<int?>(w => w.Rank);
        grouping.CountDistinct(w => w.Rank);
        grouping.CountWhere(w => w.Rank > 2);
        grouping.Having(count, CompareOp.Greater, 1L).Having(sum, CompareOp.GreaterOrEqual, 10L)
            .OrderBy(count, descending: true).OrderByKey().Take(5).Skip(10);

        Assert.Equal(
            "SELECT \"name\", count(*), COALESCE(SUM(COALESCE(\"rank\", 0)), 0), min(\"rank\"), max(\"rank\"), count(DISTINCT \"rank\"), "
            + "count(*) FILTER (WHERE (\"rank\" > @p1)) FROM \"fixture_widgets\" WHERE \"rank\" > @p0 GROUP BY \"name\" "
            + "HAVING (count(*) > @p2 AND COALESCE(SUM(COALESCE(\"rank\", 0)), 0) >= @p3) "
            + "ORDER BY count(*) DESC NULLS LAST, \"name\" ASC NULLS LAST LIMIT 5 OFFSET 10",
            grouping.RenderSelect());
    }

    [Theory(DisplayName = "an average is cast to double precision, so both providers hand the caller a double whatever numeric type the column has")]
    [MemberData(nameof(Providers))]
    public void An_average_is_a_double(string id)
    {
        var text = Widgets(Provider(id)).AggregateAvg(w => w.Rank).AggregateMax(w => w.Name).RenderAggregates();

        Assert.Equal(
            id == "postgres"
                ? "SELECT avg(\"rank\")::double precision, max(\"name\") FROM \"fixture_widgets\""
                : "SELECT CAST(avg(\"rank\") AS real), max(\"name\") FROM \"fixture_widgets\"",
            text);
    }

    [Theory(DisplayName = "several keys are grouped and ordered together, and a key with no aggregate is a plain GROUP BY")]
    [MemberData(nameof(Providers))]
    public void Several_keys_group_together(string id)
    {
        var grouping = Widgets(Provider(id)).GroupBy(w => w.Name, w => w.Rank, w => w.Id);
        grouping.OrderByKey(descending: true);

        Assert.Equal(
            "SELECT \"name\", \"rank\", \"id\" FROM \"fixture_widgets\" GROUP BY \"name\", \"rank\", \"id\" "
            + "ORDER BY \"name\" DESC NULLS LAST, \"rank\" DESC NULLS LAST, \"id\" DESC NULLS LAST",
            grouping.RenderSelect());
    }

    [Fact(DisplayName = "an aggregate handle works only on the grouping that made it, and Having refuses a NULL value rather than matching no group")]
    public void Handles_belong_to_one_grouping()
    {
        var first = Widgets(PostgresProvider.Instance).GroupBy(w => w.Name);
        var second = Widgets(PostgresProvider.Instance).GroupBy(w => w.Name);
        var count = first.Count();

        Assert.Throws<ArgumentException>(() => second.OrderBy(count));
        Assert.Throws<ArgumentException>(() => second.Having(count, CompareOp.Greater, 1L));
        Assert.Throws<ArgumentNullException>(() => first.Having(first.Min<string>(w => w.Name), CompareOp.Equal, null!));
    }

    [Fact(DisplayName = "the Select node carries the grouping and having slots as children, between WHERE and ORDER BY")]
    public void The_select_node_lists_its_grouping_slots()
    {
        var key = new ColumnRef("name");
        var having = new Compare(CompareOp.Greater, new Call(Fn.Count, [new Star()]), new IntLit(1));
        var order = new OrderBy([new OrderTerm(key, false)]);
        var select = new Select([key], new TableRef("t"), new Where([key]), order, GroupBy: [key], Having: having);

        Assert.Equal<Node>([key, new TableRef("t"), new Where([key]), key, having, order], select.Children());
    }
}
