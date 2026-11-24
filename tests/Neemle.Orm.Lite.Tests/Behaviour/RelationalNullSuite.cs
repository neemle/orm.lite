using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

// ORML-50 on both providers: a relational comparison with a null value is refused before anything runs, the same
// comparison with a value selects by value, and the NULL rows are reached through == null.
public abstract class RelationalNullSuite<TFixture>(TFixture fixture) : QueryProbeSuite<TFixture>(fixture)
    where TFixture : ProviderFixture
{
    private Task<int> SeedDueAsync(int band) => SeedAsync(
        new QueryProbe { Band = band, Due = 1, Tier = EnumTier.Low, Weight = 1 },
        new QueryProbe { Band = band, Due = 3, Tier = EnumTier.Low, Weight = 1 },
        new QueryProbe { Band = band, Due = null, Tier = EnumTier.Low, Weight = 1 });

    [SkippableFact(DisplayName =
        "ORML-50 a null floor is refused, a floor with a value selects, == null reaches the NULL row")]
    public async Task A_null_floor_is_refused_and_the_way_out_works()
    {
        await PrepareAsync();
        await SeedDueAsync(16000);
        int? none = null;
        int? floor = 2;

        Assert.Throws<NotSupportedException>(() => Probes().Where(p => p.Band == 16000).Where(p => p.Due > none));
        var above = await Probes().Where(p => p.Band == 16000).Where(p => p.Due > floor).ToListAsync();
        var missing = await Probes().Where(p => p.Band == 16000).Where(p => p.Due == none).ToListAsync();

        Assert.Equal([3], above.Select(p => p.Due));
        Assert.Equal([(int?)null], missing.Select(p => p.Due));
    }
}
