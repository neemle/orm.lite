using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

// ORML-12 (B11): a plain order puts NULLs last in both directions on both providers, so a page read through
// OrderBy(...).Take(n) holds the same rows on SQLite and on PostgreSQL; and a != or a negated == leaves a NULL row out
// the way SQL does, which the README states, with the way to ask for the NULL rows too.
public abstract class NullOrderSuite<TFixture>(TFixture fixture) : QueryProbeSuite<TFixture>(fixture)
    where TFixture : ProviderFixture
{
    private static QueryProbe Probe(int band, string? label, int? due) =>
        new() { Band = band, Label = label, Due = due, Tier = EnumTier.Low, Weight = 1 };

    private Task<int> SeedDueAsync(int band) => SeedAsync(
        Probe(band, "a", 3), Probe(band, "b", null), Probe(band, "c", 1), Probe(band, "d", 2), Probe(band, "e", null));

    [SkippableFact(DisplayName =
        "ORML-12 an ascending order puts the NULL rows last, and a page of it holds the same rows")]
    public async Task Ascending_order_puts_nulls_last()
    {
        await PrepareAsync();
        await SeedDueAsync(12000);

        var all = await Probes().Where(p => p.Band == 12000).OrderBy(p => p.Due).ThenBy(p => p.Label).ToListAsync();
        var page = await Probes().Where(p => p.Band == 12000).OrderBy(p => p.Due).ThenBy(p => p.Label).Take(3)
            .ToListAsync();

        Assert.Equal(["c", "d", "a", "b", "e"], all.Select(p => p.Label));
        Assert.Equal(["c", "d", "a"], page.Select(p => p.Label));
    }

    [SkippableFact(DisplayName = "ORML-12 a descending order puts the NULL rows last too, not first")]
    public async Task Descending_order_puts_nulls_last()
    {
        await PrepareAsync();
        await SeedDueAsync(12100);

        var all = await Probes().Where(p => p.Band == 12100).OrderByDescending(p => p.Due)
            .ThenByDescending(p => p.Label).ToListAsync();
        var first = await Probes().Where(p => p.Band == 12100).OrderByDescending(p => p.Due).Take(1).ToListAsync();

        Assert.Equal(["a", "d", "c", "e", "b"], all.Select(p => p.Label));
        Assert.Equal(["a"], first.Select(p => p.Label));
    }

    [SkippableFact(DisplayName = "ORML-12 a != leaves the NULL row out as SQL does, and the NULL test brings it back")]
    public async Task Inequality_leaves_a_null_row_out_unless_the_null_is_named()
    {
        await PrepareAsync();
        await SeedAsync(Probe(12200, "a", 1), Probe(12200, "b", 2), Probe(12200, null, 3));

        var plain = await Probes().Where(p => p.Band == 12200).Where(p => p.Label != "a").ToListAsync();
        var negated = await Probes().Where(p => p.Band == 12200).Where(p => !(p.Label == "a")).ToListAsync();
        var named = await Probes().Where(p => p.Band == 12200)
            .Where(p => p.Label == null || p.Label != "a").OrderBy(p => p.Due).ToListAsync();

        Assert.Equal([2], plain.Select(p => p.Due));
        Assert.Equal([2], negated.Select(p => p.Due));
        Assert.Equal([2, 3], named.Select(p => p.Due));
    }
}
