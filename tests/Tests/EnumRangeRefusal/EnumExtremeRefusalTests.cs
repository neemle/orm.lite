using Neemle.OrmLite.Tests.ClosureOperators;
using Xunit;

namespace Neemle.OrmLite.Tests.EnumRangeRefusal;

// ORML-83 (follow-up of ORML-11): Min and Max over an enum column, and an order by the keys of a grouping whose key is
// an enum column, would answer with the alphabetical member NAME or order, so they are refused by name like the range
// and the OrderBy of ORML-11. Every case runs against the PostgreSQL and the SQLite provider.
public class EnumExtremeRefusalTests
{
    private const string Status = "Status (FixtureStatus)";

    private const string Review = "Review (FixtureStatus?)";

    public static TheoryData<string> Providers => ClosureItems.Providers;

    private static Query<ClosureItem> Items(string provider) => ClosureItems.Of(provider);

    private static NotSupportedException Refused(Func<object> build) => Assert.Throws<NotSupportedException>(build);

    private static void Extreme(NotSupportedException error, string function, string column)
    {
        Assert.Contains($"Cannot take the {function} of the enum column {column}:", error.Message);
        Assert.Contains("alphabetically and not by value", error.Message);
        Assert.Contains("Nothing was executed.", error.Message);
    }

    private static void KeyOrder(NotSupportedException error, string column)
    {
        Assert.Contains($"Cannot order the groups by the enum key {column}:", error.Message);
        Assert.Contains("alphabetically and not by value", error.Message);
        Assert.Contains("Nothing was executed.", error.Message);
    }

    [Theory(DisplayName = "AC1 AggregateMin and AggregateMax over an enum, nullable enum or int cast are refused")]
    [MemberData(nameof(Providers))]
    public void A_query_minimum_or_maximum_over_an_enum_column_is_refused(string provider)
    {
        Extreme(Refused(() => Items(provider).AggregateMin(n => n.Status)), "Min", Status);
        Extreme(Refused(() => Items(provider).AggregateMax(n => n.Status)), "Max", Status);
        Extreme(Refused(() => Items(provider).AggregateMin(n => n.Review)), "Min", Review);
        Extreme(Refused(() => Items(provider).AggregateMax(n => (int)n.Status)), "Max", Status);
    }

    [Theory(DisplayName = "AC1 a grouping's Min and Max over an enum column are refused, also for a nullable enum")]
    [MemberData(nameof(Providers))]
    public void A_grouping_minimum_or_maximum_over_an_enum_column_is_refused(string provider)
    {
        var grouping = Items(provider).GroupBy(n => n.Rank);

        Extreme(Refused(() => grouping.Min<FixtureStatus>(n => n.Status)), "Min", Status);
        Extreme(Refused(() => grouping.Max<FixtureStatus>(n => n.Status)), "Max", Status);
        Extreme(Refused(() => grouping.Min<FixtureStatus?>(n => n.Review)), "Min", Review);
        Extreme(Refused(() => grouping.Max<FixtureStatus?>(n => n.Review)), "Max", Review);
    }

    [Theory(DisplayName = "AC1 a view column's Min over an enum column is refused before the view is defined")]
    [MemberData(nameof(Providers))]
    public void A_view_minimum_over_an_enum_column_is_refused(string provider)
    {
        var refused = Refused(() => Items(provider).AsView<ClosureItem>(c => c.Min(v => v.Rank, n => n.Status)));

        Extreme(refused, "Min", Status);
    }

    [Theory(DisplayName = "AC1 an order by the key of a grouping is refused when any key is an enum column")]
    [MemberData(nameof(Providers))]
    public void An_order_by_an_enum_group_key_is_refused(string provider)
    {
        var one = Refused(() => Items(provider).GroupBy(n => n.Status).OrderByKey());
        var descending = Refused(() => Items(provider).GroupBy(n => n.Status).OrderByKey(descending: true));
        var nullable = Refused(() => Items(provider).GroupBy(n => n.Review).OrderByKey());
        var second = Refused(() => Items(provider).GroupBy(n => n.Rank, n => n.Status).OrderByKey());
        var third = Refused(() => Items(provider).GroupBy(n => n.Rank, n => n.Title, n => n.Review).OrderByKey());

        KeyOrder(one, Status);
        KeyOrder(descending, Status);
        KeyOrder(nullable, Review);
        KeyOrder(second, Status);
        KeyOrder(third, Review);
    }

    [Theory(DisplayName = "the refusal names what to do instead and never a stored member name")]
    [MemberData(nameof(Providers))]
    public void The_refusal_gives_the_way_out(string provider)
    {
        var extreme = Refused(() => Items(provider).AggregateMin(n => n.Status));
        var key = Refused(() => Items(provider).GroupBy(n => n.Status).OrderByKey());

        Assert.Contains("take it over the enum values in memory", extreme.Message);
        Assert.Contains("sort the enum values in memory", key.Message);
        Assert.DoesNotContain("pending", extreme.Message);
        Assert.DoesNotContain("pending", key.Message);
    }

    [Theory(DisplayName = "Min, Max and the key order over non-enum columns, and a GroupBy on an enum, still render")]
    [MemberData(nameof(Providers))]
    public void What_does_not_rank_an_enum_still_renders(string provider)
    {
        var extremes = Items(provider).AggregateMin(n => n.Rank).AggregateMax(n => n.Title);
        var keyed = Items(provider).GroupBy(n => n.Rank);
        keyed.Count();
        var enumKey = Items(provider).GroupBy(n => n.Status);
        var count = enumKey.Count();
        enumKey.OrderBy(count, descending: true);

        Assert.Equal(
            "SELECT min(\"rank\"), max(\"title\") FROM \"closure_items\"", extremes.RenderAggregates());
        Assert.Equal(
            "SELECT \"rank\", count(*) FROM \"closure_items\" GROUP BY \"rank\" ORDER BY \"rank\" ASC NULLS LAST",
            keyed.OrderByKey().RenderSelect());
        Assert.Equal(
            "SELECT \"status\", count(*) FROM \"closure_items\" GROUP BY \"status\" ORDER BY count(*) DESC NULLS LAST",
            enumKey.RenderSelect());
    }
}
