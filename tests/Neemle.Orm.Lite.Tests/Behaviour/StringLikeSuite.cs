using System.Globalization;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

// ORML-15 (B17): the StartsWith and EndsWith overloads that take a bool and a culture ignore case when the bool says
// so, on both providers; before, they were always case-sensitive because only the two-argument form was looked at.
public abstract class StringLikeSuite<TFixture>(TFixture fixture) : QueryProbeSuite<TFixture>(fixture)
    where TFixture : ProviderFixture
{
    private Task<int> SeedLabelsAsync(int band) => SeedAsync(
        new QueryProbe { Band = band, Label = "Alpha", Tier = EnumTier.Low, Weight = 1 },
        new QueryProbe { Band = band, Label = "beta", Tier = EnumTier.Low, Weight = 1 });

    [SkippableFact(DisplayName =
        "ORML-15 StartsWith and EndsWith with ignoreCase true and a culture match regardless of case")]
    public async Task The_culture_overloads_ignore_case_when_asked()
    {
        await PrepareAsync();
        await SeedLabelsAsync(15000);
        var culture = CultureInfo.InvariantCulture;

        var starts = await Probes().Where(p => p.Band == 15000)
            .Where(p => p.Label!.StartsWith("AL", true, culture)).ToListAsync();
        var ends = await Probes().Where(p => p.Band == 15000)
            .Where(p => p.Label!.EndsWith("HA", true, culture)).ToListAsync();

        Assert.Equal(["Alpha"], starts.Select(p => p.Label));
        Assert.Equal(["Alpha"], ends.Select(p => p.Label));
    }
}
