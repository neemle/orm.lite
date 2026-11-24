using System.Runtime.CompilerServices;
using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

// ORML-11 (B10): a range or an ordering over an enum column is refused on both providers, and what is not a range
// or an ordering keeps working. Each test seeds its own rows in its own rank band, so tests do not see each other.
public abstract class EnumOrderSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private static readonly ConditionalWeakTable<ProviderFixture, Task> Prepared = new();

    private static readonly EnumTier[] Tiers = [EnumTier.Low, EnumTier.Mid, EnumTier.High];

    private OrmContext Ctx => fixture.Context;

    private Query<EnumProbe> Probes() => new(Ctx, EnumProbe.Map);

    private async Task PrepareAsync()
    {
        fixture.RequireAvailable();
        EnumProbeWorld.Register();
        await Prepared.GetValue(fixture, _ => SetUpAsync());
    }

    private Task SetUpAsync() =>
        MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, [EnumProbeWorld.Schema()]);

    // Three rows, Low, Mid and High, holding the ranks band + 1, band + 2 and band + 3.
    private async Task<List<Guid>> SeedAsync(int band)
    {
        var ids = Tiers.Select(_ => Guid.NewGuid()).ToList();
        await Probes().InsertManyAsync(Tiers.Select((tier, i) => new Row<EnumProbe>()
            .With(x => x.Id, ids[i])
            .With(x => x.Tier, tier)
            .With(x => x.Rank, band + 1 + i)).ToList());
        return ids;
    }

    private async Task<List<EnumTier>> TiersAsync(List<Guid> ids, Query<EnumProbe> query)
    {
        var rows = await query.Where(p => ids.Contains(p.Id)).ToListAsync();
        return rows.Select(p => p.Tier).Order().ToList();
    }

    [SkippableFact(DisplayName = "B10 premise: the stored member names rank alphabetically on this provider")]
    public async Task Stored_member_names_rank_alphabetically_and_not_by_value()
    {
        await PrepareAsync();
        await SeedAsync(9100);

        var rows = await fixture.RowsAsync(
            "SELECT \"tier\" FROM \"enum_probe_rows\" WHERE \"rank\" > 9100 AND \"rank\" < 9200 ORDER BY \"tier\"");

        Assert.Equal(["High", "Low", "Mid"], rows.Select(r => (string)r[0]!).ToList());
    }

    [SkippableFact(DisplayName = "AC1 a range over an enum column is refused by name and reads no row")]
    public async Task A_range_over_an_enum_column_is_refused()
    {
        await PrepareAsync();
        await SeedAsync(9200);

        var named = Assert.Throws<NotSupportedException>(() => Probes().Where(p => p.Tier >= EnumTier.Mid));
        var cast = Assert.Throws<NotSupportedException>(() => Probes().Where(p => (int)p.Tier > 0));
        var left = Assert.Throws<NotSupportedException>(() => Probes().Where(p => EnumTier.Mid > p.Tier));

        Assert.Contains("Cannot compare the enum column Tier (EnumTier) with >=:", named.Message);
        Assert.Contains("Cannot compare the enum column Tier (EnumTier) with >:", cast.Message);
        Assert.Contains("Cannot compare the enum column Tier (EnumTier) with >:", left.Message);
    }

    [SkippableFact(DisplayName = "AC2 an ordering over an enum column is refused by name, all four methods")]
    public async Task An_ordering_over_an_enum_column_is_refused()
    {
        await PrepareAsync();
        await SeedAsync(9300);

        var errors = new[]
        {
            Assert.Throws<NotSupportedException>(() => Probes().OrderBy(p => p.Tier)),
            Assert.Throws<NotSupportedException>(() => Probes().OrderByDescending(p => p.Tier)),
            Assert.Throws<NotSupportedException>(() => Probes().OrderBy(p => p.Rank).ThenBy(p => p.Tier)),
            Assert.Throws<NotSupportedException>(() => Probes().OrderBy(p => p.Rank).ThenByDescending(p => p.Tier)),
        };

        Assert.All(errors, e => Assert.Contains("Cannot order by the enum column Tier (EnumTier):", e.Message));
    }

    [SkippableFact(DisplayName = "equality, inequality and membership on an enum column still select the right rows")]
    public async Task Equality_and_membership_on_an_enum_column_still_work()
    {
        await PrepareAsync();
        var ids = await SeedAsync(9400);
        List<EnumTier> upper = [EnumTier.Mid, EnumTier.High];

        Assert.Equal([EnumTier.Mid], await TiersAsync(ids, Probes().Where(p => p.Tier == EnumTier.Mid)));
        Assert.Equal([EnumTier.Low, EnumTier.High], await TiersAsync(ids, Probes().Where(p => p.Tier != EnumTier.Mid)));
        Assert.Equal([EnumTier.Mid, EnumTier.High], await TiersAsync(ids, Probes().Where(p => upper.Contains(p.Tier))));
    }

    [SkippableFact(DisplayName = "the way out works: a range and an order over the integer rank select by value")]
    public async Task A_rank_column_ranges_and_orders_by_value()
    {
        await PrepareAsync();
        var ids = await SeedAsync(9500);

        var upper = await Probes().Where(p => ids.Contains(p.Id)).Where(p => p.Rank >= 9502).ToListAsync();
        var ordered = await Probes().Where(p => ids.Contains(p.Id)).OrderByDescending(p => p.Rank).ToListAsync();

        Assert.Equal([EnumTier.Mid, EnumTier.High], upper.Select(p => p.Tier).Order().ToList());
        Assert.Equal([EnumTier.High, EnumTier.Mid, EnumTier.Low], ordered.Select(p => p.Tier).ToList());
    }

    // Band 0 seeds the ranks 1, 2 and 3 for Low, Mid and High; EnumTier.High has the value 2, so the captured
    // variable's integer value is the rank floor. The other tests use bands above 9100 and filter by their own ids.
    [SkippableFact(DisplayName =
        "ORML-82 an integer column ranges against (int) of a captured enum variable and selects by value")]
    public async Task A_rank_column_ranges_against_a_captured_enum_as_an_integer()
    {
        await PrepareAsync();
        var ids = await SeedAsync(0);
        var floor = EnumTier.High;

        var rows = await Probes().Where(p => ids.Contains(p.Id)).Where(p => p.Rank >= (int)floor).ToListAsync();

        Assert.Equal([EnumTier.Mid, EnumTier.High], rows.Select(p => p.Tier).Order().ToList());
    }

    [SkippableFact(DisplayName = "ORML-83 Min and Max over an enum column are refused by name, query and grouping")]
    public async Task Min_and_max_over_an_enum_column_are_refused()
    {
        await PrepareAsync();
        await SeedAsync(9600);

        var errors = new[]
        {
            Assert.Throws<NotSupportedException>(() => Probes().AggregateMin(p => p.Tier)),
            Assert.Throws<NotSupportedException>(() => Probes().AggregateMax(p => p.Tier)),
            Assert.Throws<NotSupportedException>(() => Probes().GroupBy(p => p.Rank).Min<EnumTier>(p => p.Tier)),
            Assert.Throws<NotSupportedException>(() => Probes().GroupBy(p => p.Rank).Max<EnumTier>(p => p.Tier)),
        };

        Assert.Contains("Cannot take the Min of the enum column Tier (EnumTier):", errors[0].Message);
        Assert.Contains("Cannot take the Max of the enum column Tier (EnumTier):", errors[1].Message);
        Assert.Contains("Cannot take the Min of the enum column Tier (EnumTier):", errors[2].Message);
        Assert.Contains("Cannot take the Max of the enum column Tier (EnumTier):", errors[3].Message);
    }

    [SkippableFact(DisplayName = "ORML-83 an order by an enum group key is refused, any other key still orders")]
    public async Task An_order_by_an_enum_group_key_is_refused()
    {
        await PrepareAsync();
        var ids = await SeedAsync(9700);

        var one = Assert.Throws<NotSupportedException>(() => Probes().GroupBy(p => p.Tier).OrderByKey());
        var two = Assert.Throws<NotSupportedException>(
            () => Probes().GroupBy(p => p.Rank, p => p.Tier).OrderByKey(descending: true));
        var grouping = Probes().Where(p => ids.Contains(p.Id)).GroupBy(p => p.Rank);
        var rows = await grouping.OrderByKey(descending: true).ToListAsync();

        Assert.Contains("Cannot order the groups by the enum key Tier (EnumTier):", one.Message);
        Assert.Contains("Cannot order the groups by the enum key Tier (EnumTier):", two.Message);
        Assert.Equal([9703, 9702, 9701], rows.Select(r => r.Key));
    }

    [SkippableFact(DisplayName = "ORML-83 the way out works: Min and Max over the integer rank answer by value")]
    public async Task The_rank_column_takes_min_and_max_by_value()
    {
        await PrepareAsync();
        var ids = await SeedAsync(9800);

        var row = await Probes().Where(p => ids.Contains(p.Id))
            .AggregateMin(p => p.Rank).AggregateMax(p => p.Rank).AggregatesAsync();

        Assert.Equal([9801, 9803], row.Select(value => Convert.ToInt32(value)));
    }
}
