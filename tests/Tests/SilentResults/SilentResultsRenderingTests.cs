using Neemle.OrmLite.Tests.ClosureOperators;
using Xunit;

namespace Neemle.OrmLite.Tests.SilentResults;

// ORML-14 (B13 to B16): the query layer used to answer wrongly without a word; each case is now masked, refused by
// name or counted from zero. The translator is provider-neutral, so each case runs on both providers.
public class SilentResultsRenderingTests
{
    private const string From = " FROM \"closure_items\"";

    [Theory(DisplayName = "ORML-14 B13 a projection, a projection list and a scalar return the mask, not the column")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void The_mask_reaches_every_read_that_returns_the_column(string provider)
    {
        var project = ClosureItems.Of(provider).Redact(n => n.Title).RenderProject([n => n.Title, n => n.Rank]);
        var terms = ClosureItems.Of(provider).Redact(n => n.Title).ProjectColumn(n => n.Title)
            .ProjectColumn(n => n.Rank).RenderProjection();
        var scalar = ClosureItems.Of(provider).Redact(n => n.Title).RenderScalar(n => n.Title);

        Assert.Equal("SELECT NULL, \"rank\"" + From, project);
        Assert.Equal("SELECT NULL, \"rank\"" + From, terms);
        Assert.Equal("SELECT NULL" + From + " LIMIT 1", scalar);
    }

    [Theory(DisplayName =
        "ORML-14 B13 a conditional mask applies in a projection, a column that is not masked does not")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_conditional_mask_applies_in_a_projection(string provider)
    {
        var masked = ClosureItems.Of(provider).RedactWhen(n => n.Title, n => n.Rank > 3)
            .RenderProject([n => n.Title, n => n.Rank]);

        Assert.Equal("SELECT CASE WHEN \"rank\" > @p0 THEN NULL ELSE \"title\" END, \"rank\"" + From, masked);
    }

    [Theory(DisplayName = "ORML-14 B13 an aggregate or a grouping over a masked column is refused by name")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void An_aggregate_over_a_masked_column_is_refused(string provider)
    {
        var errors = new[]
        {
            Assert.Throws<NotSupportedException>(() => Masked(provider).AggregateMin(n => n.Title)),
            Assert.Throws<NotSupportedException>(() => Masked(provider).AggregateMax(n => n.Title)),
            Assert.Throws<NotSupportedException>(() => Masked(provider).AggregateCountDistinct(n => n.Title)),
            Assert.Throws<NotSupportedException>(() => Masked(provider).GroupBy(n => n.Title)),
            Assert.Throws<NotSupportedException>(() => Masked(provider).GroupBy(n => n.Rank).Max<string>(n => n.Title)),
        };

        Assert.All(errors, e => Assert.Contains("Cannot aggregate or group by the column Title:", e.Message));
    }

    [Theory(DisplayName = "ORML-14 B13 a column that is not masked, and a filter on a masked one, work as before")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void What_is_not_masked_is_unchanged(string provider)
    {
        var aggregate = Masked(provider).AggregateMin(n => n.Rank).RenderAggregates();
        var filtered = Masked(provider).Where(n => n.Title == "x").RenderSelect();

        Assert.Equal("SELECT min(\"rank\")" + From, aggregate);
        Assert.EndsWith(" WHERE \"title\" = @p0", filtered);
    }

    [Theory(DisplayName = "ORML-14 B14 SumAsync refuses a double and a decimal column by name, before anything runs")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public async Task SumAsync_refuses_a_fractional_column(string provider)
    {
        var floating = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ClosureItems.Of(provider).SumAsync(n => n.Score));
        var exact = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ClosureItems.Of(provider).SumAsync(n => n.Price));

        Assert.Contains("would round a double or float column", floating.Message);
        Assert.Contains("a sum of 0.4 would read as 0 and a sum of 2.5 as 2", floating.Message);
        Assert.Contains("AggregateSum", floating.Message);
        Assert.Contains("would round a decimal column", exact.Message);
    }

    [Theory(DisplayName =
        "ORML-14 B16 a DoUpdate without a set is refused by name, DoNothing and a DoUpdate with sets render")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void A_do_update_without_a_set_is_refused(string provider)
    {
        var row = new Row<ClosureItem>().With(n => n.Id, Guid.NewGuid()).With(n => n.Rank, 1);
        UpsertSet<ClosureItem>[] sets = [UpsertSet<ClosureItem>.Inserted(n => n.Rank)];

        var refusal = Assert.Throws<InvalidOperationException>(
            () => ClosureItems.Of(provider).RenderUpsert(row, [n => n.Id], OnConflict.DoUpdate));
        var empty = Assert.Throws<InvalidOperationException>(
            () => ClosureItems.Of(provider).RenderUpsert(row, [n => n.Id], OnConflict.DoUpdate, []));

        Assert.Contains("OnConflict.DoUpdate names no UpsertSet", refusal.Message);
        Assert.Contains("OnConflict.DoNothing", refusal.Message);
        Assert.Equal(refusal.Message, empty.Message);
        var nothing = ClosureItems.Of(provider).RenderUpsert(row, [n => n.Id], OnConflict.DoNothing).Sql;
        var update = ClosureItems.Of(provider).RenderUpsert(row, [n => n.Id], OnConflict.DoUpdate, sets).Sql;
        Assert.EndsWith("DO NOTHING", nothing);
        Assert.Contains("DO UPDATE SET", update);
    }

    [Theory(DisplayName = "ORML-14 B15 an upsert Increment counts a NULL counter from zero on both providers")]
    [MemberData(nameof(ClosureItems.Providers), MemberType = typeof(ClosureItems))]
    public void An_upsert_increment_is_coalesced(string provider)
    {
        var row = new Row<ClosureItem>().With(n => n.Id, Guid.NewGuid()).With(n => n.Rank, 1);
        UpsertSet<ClosureItem>[] sets = [UpsertSet<ClosureItem>.Increment(n => n.Stars, 2)];

        var sql = ClosureItems.Of(provider).RenderUpsert(row, [n => n.Id], OnConflict.DoUpdate, sets).Sql;

        Assert.EndsWith(
            "DO UPDATE SET \"stars\" = COALESCE(\"closure_items\".\"stars\", 0) + 2", sql, StringComparison.Ordinal);
    }

    private static Query<ClosureItem> Masked(string provider) => ClosureItems.Of(provider).Redact(n => n.Title);
}
