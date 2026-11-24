using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

public abstract class GroupingBehaviourSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private OrmContext Ctx => fixture.Context;

    private Query<BehaveNote> Notes() => new(Ctx, BehaveNote.Map);

    private async Task<Guid> SeedAsync(params (string Title, int Stars, decimal? Price)[] notes)
    {
        var tenant = Guid.NewGuid();
        await Notes().InsertManyAsync(notes.Select(n => new Row<BehaveNote>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.TenantId, tenant)
            .With(x => x.Title, n.Title)
            .With(x => x.Stars, n.Stars)
            .With(x => x.Price, n.Price)).ToList());
        return tenant;
    }

    private static (string, int, decimal?)[] SixNotes() =>
    [
        ("a", 1, 10.5m), ("b", 1, 20.5m), ("c", 2, null), ("d", 3, 1.25m), ("e", 3, 2.25m), ("f", 3, 3.5m),
    ];

    [SkippableFact(DisplayName = "one statement per group returns the count, sum, min, max, average and filtered count of each group, computed by the database")]
    public async Task Every_aggregate_is_computed_per_group()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync(SixNotes());

        var grouping = Notes().Where(n => n.TenantId == tenant).GroupBy(n => n.Stars);
        var count = grouping.Count();
        var stars = grouping.Sum<long>(n => n.Stars);
        var first = grouping.Min<string>(n => n.Title);
        var last = grouping.Max<string>(n => n.Title);
        var average = grouping.Avg(n => n.Stars);
        var priced = grouping.CountWhere(n => n.Price != null);
        var rows = await grouping.OrderByKey().ToListAsync();

        Assert.Equal([1, 2, 3], rows.Select(r => r.Key));
        Assert.Equal([2L, 1L, 3L], rows.Select(r => r.Get(count)));
        Assert.Equal([2L, 2L, 9L], rows.Select(r => r.Get(stars)));
        Assert.Equal(["a", "c", "d"], rows.Select(r => r.Get(first)));
        Assert.Equal(["b", "c", "f"], rows.Select(r => r.Get(last)));
        Assert.Equal([1.0, 2.0, 3.0], rows.Select(r => r.Get(average)));
        Assert.Equal([2L, 0L, 3L], rows.Select(r => r.Get(priced)));
    }

    [SkippableFact(DisplayName = "an exact average of a decimal column rounded to a stated scale, and a min, max and sum of it, come back as exact decimals, and a group with no value gives null for min, max and average")]
    public async Task Decimal_columns_aggregate()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync(SixNotes());

        var grouping = Notes().Where(n => n.TenantId == tenant).GroupBy(n => n.Stars);
        var average = grouping.AvgDecimal(n => n.Price, 4);
        var min = grouping.Min<decimal?>(n => n.Price);
        var max = grouping.Max<decimal?>(n => n.Price);
        var sum = grouping.Sum<decimal>(n => n.Price);
        var rows = await grouping.OrderByKey().ToListAsync();

        Assert.Equal([15.5m, null, 2.3333m], rows.Select(r => r.Get(average)));
        Assert.Equal([10.5m, null, 1.25m], rows.Select(r => r.Get(min)));
        Assert.Equal([20.5m, null, 3.5m], rows.Select(r => r.Get(max)));
        Assert.Equal([31.0m, 0m, 7.0m], rows.Select(r => r.Get(sum)));
    }

    [SkippableFact(DisplayName = "per group, 0.1 plus 0.2 sums to exactly 0.3, and Having and ordering compare decimal sums by value: 10 is above 9.99")]
    public async Task Decimal_sums_per_group_are_exact_and_compare_numerically()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync(("a", 1, 0.1m), ("b", 1, 0.2m), ("c", 2, 9.99m), ("d", 3, 10m));

        var grouping = Notes().Where(n => n.TenantId == tenant).GroupBy(n => n.Stars);
        var sum = grouping.Sum<decimal>(n => n.Price);
        var all = await grouping.OrderBy(sum, descending: true).ToListAsync();

        var filtered = Notes().Where(n => n.TenantId == tenant).GroupBy(n => n.Stars);
        var filteredSum = filtered.Sum<decimal>(n => n.Price);
        var kept = await filtered.Having(filteredSum, CompareOp.GreaterOrEqual, 9.99m).OrderByKey().ToListAsync();

        var exact = Notes().Where(n => n.TenantId == tenant).GroupBy(n => n.Stars);
        var exactSum = exact.Sum<decimal>(n => n.Price);
        var isThirty = await exact.Having(exactSum, CompareOp.Equal, 0.3m).ToListAsync();

        Assert.Equal([3, 2, 1], all.Select(r => r.Key));
        Assert.Equal([10m, 9.99m, 0.3m], all.Select(r => r.Get(sum)));
        Assert.Equal([2, 3], kept.Select(r => r.Key));
        Assert.Equal([1], isThirty.Select(r => r.Key));
    }

    [SkippableFact(DisplayName = "the whole-table aggregates over a decimal column are exact: the sum of 0.1 and 0.2 is 0.3, the max is numeric (10 above 9.99) and the average is rounded to the stated scale")]
    public async Task Whole_table_decimal_aggregates_are_exact()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync(("a", 1, 0.1m), ("b", 1, 0.2m), ("c", 2, 9.99m), ("d", 3, 10m));

        var row = await Notes().Where(n => n.TenantId == tenant)
            .AggregateSum(n => n.Price).AggregateMin(n => n.Price).AggregateMax(n => n.Price).AggregateAvgDecimal(n => n.Price, 3)
            .AggregatesAsync();
        var small = await Notes().Where(n => n.TenantId == tenant && n.Stars == 1).AggregateSum(n => n.Price).AggregatesAsync();

        Assert.Equal(20.29m, row[0]);
        Assert.Equal(0.1m, row[1]);
        Assert.Equal(10m, row[2]);
        Assert.Equal(5.073m, row[3]);
        Assert.Equal(0.3m, small[0]);
    }

    [SkippableFact(DisplayName = "an average of a decimal column through Avg is refused and names AvgDecimal, and AvgDecimal refuses a scale outside 0 to 28")]
    public Task Avg_of_a_decimal_is_refused()
    {
        fixture.RequireAvailable();
        var grouping = Notes().GroupBy(n => n.Stars);

        var refusal = Assert.Throws<InvalidOperationException>(() => grouping.Avg(n => n.Price));
        Assert.Contains("AvgDecimal", refusal.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => grouping.AvgDecimal(n => n.Price, 29));
        Assert.Throws<InvalidOperationException>(() => Notes().AggregateAvg(n => n.Price));
        return Task.CompletedTask;
    }

    [SkippableFact(DisplayName = "Having keeps the groups whose aggregate passes and the result is ordered by an aggregate, then by the key")]
    public async Task Having_and_order_by_an_aggregate()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync(SixNotes());

        var big = Notes().Where(n => n.TenantId == tenant).GroupBy(n => n.Stars);
        var bigCount = big.Count();
        var bigSum = big.Sum<long>(n => n.Stars);
        var kept = await big.Having(bigCount, CompareOp.GreaterOrEqual, 2L).OrderBy(bigSum, descending: true).ToListAsync();
        var small = Notes().Where(n => n.TenantId == tenant).GroupBy(n => n.Stars);
        var smallSum = small.Sum<long>(n => n.Stars);
        var narrowed = await small.Having(smallSum, CompareOp.Less, 5L).OrderByKey(descending: true).ToListAsync();

        Assert.Equal([3, 1], kept.Select(r => r.Key));
        Assert.Equal([2, 1], narrowed.Select(r => r.Key));
    }

    [SkippableFact(DisplayName = "a NULL key is one group of its own, and ordering puts it last on both providers")]
    public async Task A_null_key_is_a_group()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync(SixNotes());

        var grouping = Notes().Where(n => n.TenantId == tenant).GroupBy(n => n.Price);
        var count = grouping.Count();
        var ascending = await grouping.OrderByKey().ToListAsync();
        var descending = await Notes().Where(n => n.TenantId == tenant).GroupBy(n => n.Price).OrderByKey(descending: true).ToListAsync();

        Assert.Equal(6, ascending.Count);
        Assert.Null(ascending[^1].Key);
        Assert.Null(descending[^1].Key);
        Assert.Equal(1.25m, ascending[0].Key);
        Assert.Equal(1, ascending.Count(r => r.Key is null));
        Assert.Equal(5, ascending.Count(r => r.Key is not null));
        Assert.Equal(1L, ascending[^1].Get(count));
    }

    [SkippableFact(DisplayName = "a composite key groups on every part, and a key-only grouping lists the distinct values")]
    public async Task A_composite_key_groups_on_each_part()
    {
        fixture.RequireAvailable();
        var tenantA = await SeedAsync(SixNotes());
        var tenantB = await SeedAsync(("x", 1, null), ("y", 1, null), ("z", 9, null));

        var grouping = Notes().Where(n => n.TenantId == tenantA || n.TenantId == tenantB).GroupBy(n => n.TenantId, n => n.Stars);
        var count = grouping.Count();
        var rows = await grouping.OrderBy(count, descending: true).OrderByKey().ToListAsync();
        var distinct = await Notes().Where(n => n.TenantId == tenantB).GroupBy(n => n.Stars).OrderByKey().ToListAsync();

        Assert.Equal(5, rows.Count);
        Assert.Equal((tenantA, 3), rows[0].Key);
        Assert.Equal(3L, rows[0].Get(count));
        Assert.Contains(rows, r => r.Key == (tenantB, 1) && r.Get(count) == 2L);
        Assert.Contains(rows, r => r.Key == (tenantB, 9) && r.Get(count) == 1L);
        Assert.Equal([1, 9], distinct.Select(r => r.Key));
    }

    [SkippableFact(DisplayName = "a grouping over no rows is an empty list, and a soft-deleted row is not counted")]
    public async Task An_empty_set_has_no_groups_and_deleted_rows_do_not_count()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync(SixNotes());
        await Notes().Where(n => n.TenantId == tenant && n.Stars == 3).DeleteAsync();

        var absent = Guid.NewGuid();
        var none = await Notes().Where(n => n.TenantId == absent).GroupBy(n => n.Stars).ToListAsync();
        var live = Notes().Where(n => n.TenantId == tenant).GroupBy(n => n.Stars);
        var count = live.Count();
        var liveRows = await live.OrderByKey().ToListAsync();
        var all = Notes().IncludingDeleted().Where(n => n.TenantId == tenant).GroupBy(n => n.Stars);
        var allCount = all.Count();
        var allRows = await all.OrderByKey(descending: true).ToListAsync();

        Assert.Empty(none);
        Assert.Equal([1, 2], liveRows.Select(r => r.Key));
        Assert.Equal(2L, liveRows[0].Get(count));
        Assert.Equal([3, 2, 1], allRows.Select(r => r.Key));
        Assert.Equal(3L, allRows[0].Get(allCount));
    }

    [SkippableFact(DisplayName = "a grouping filters through Where, WhereIn over a filtered subquery and WhereExists before it groups")]
    public async Task A_grouping_over_filtered_and_subquery_rows()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync(SixNotes());
        var ids = await Notes().Where(n => n.TenantId == tenant).OrderBy(n => n.Title).ToListAsync();
        await new Query<BehaveCounter>(Ctx, BehaveCounter.Map).InsertManyAsync(ids.Select((n, i) =>
            new Row<BehaveCounter>().With(c => c.Id, n.Id).With(c => c.N, i)).ToList());

        var viaIn = Notes().Where(n => n.TenantId == tenant)
            .WhereIn<BehaveCounter>(n => n.Id, sub => sub.Where(c => c.N >= 2), c => c.Id).GroupBy(n => n.Stars);
        var inCount = viaIn.Count();
        var viaExists = Notes().Where(n => n.TenantId == tenant)
            .WhereExists<BehaveCounter>((n, c) => c.Id == n.Id && c.N < 2).GroupBy(n => n.Stars);
        var existsCount = viaExists.Count();

        var inRows = await viaIn.OrderByKey().ToListAsync();
        var existsRows = await viaExists.OrderByKey().ToListAsync();

        Assert.Equal([(2, 1L), (3, 3L)], inRows.Select(r => (r.Key, r.Get(inCount))));
        Assert.Equal([(1, 2L)], existsRows.Select(r => (r.Key, r.Get(existsCount))));
    }

    [SkippableFact(DisplayName = "Take and Skip page the groups, and the whole-table Max and Avg terms return a double average")]
    public async Task Paging_and_the_whole_table_max_and_average()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync(SixNotes());

        var grouping = Notes().Where(n => n.TenantId == tenant).GroupBy(n => n.Stars);
        grouping.Count();
        var page = await grouping.OrderByKey().Skip(1).Take(1).ToListAsync();
        var whole = await Notes().Where(n => n.TenantId == tenant).AggregateMax(n => n.Stars).AggregateAvg(n => n.Stars).AggregatesAsync();

        Assert.Equal([2], page.Select(r => r.Key));
        Assert.Equal(3, Convert.ToInt32(whole[0]));
        Assert.IsType<double>(whole[1]);
        Assert.Equal(13.0 / 6.0, (double)whole[1]!, 9);
    }

    [SkippableFact(DisplayName = "reading a NULL aggregate into a type that cannot hold NULL is refused and names the nullable form")]
    public async Task A_null_aggregate_into_a_non_nullable_type_is_refused()
    {
        fixture.RequireAvailable();
        var tenant = await SeedAsync(("only", 1, null));

        var grouping = Notes().Where(n => n.TenantId == tenant).GroupBy(n => n.Stars);
        grouping.Max<decimal>(n => n.Price);

        var refusal = await Assert.ThrowsAsync<InvalidCastException>(() => grouping.ToListAsync());
        Assert.Contains("Decimal?", refusal.Message, StringComparison.Ordinal);
    }
}

public sealed class SqliteGroupingTests(SqliteFixture fixture) : GroupingBehaviourSuite<SqliteFixture>(fixture);

public sealed class PostgresGroupingTests(PostgresFixture fixture) : GroupingBehaviourSuite<PostgresFixture>(fixture);
