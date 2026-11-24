using System.Data.Common;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

// ORML-14 (B13 to B16) on both providers: Redact masks every read that returns the column, SumAsync refuses a
// fractional column, an upsert Increment counts a NULL counter from zero and a DoUpdate without a set is refused.
public abstract class SilentResultsSuite<TFixture>(TFixture fixture) : QueryProbeSuite<TFixture>(fixture)
    where TFixture : ProviderFixture
{
    private static QueryProbe Probe(int band, string label, string secret) =>
        new() { Band = band, Label = label, Secret = secret, Tier = EnumTier.Low, Weight = 0.4 };

    private static string? Text(DbDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetString(ordinal);

    private Query<QueryProbe> InBand(int band) => Probes().Where(p => p.Band == band);

    [SkippableFact(DisplayName = "ORML-14 B13 a projection and a scalar return the mask, and the rows still do")]
    public async Task Redact_masks_a_projection_and_a_scalar()
    {
        await PrepareAsync();
        await SeedAsync(Probe(14000, "L", "s3cret"));

        var open = await InBand(14000).ProjectAsync([p => p.Secret], r => Text(r, 0));
        var masked = await InBand(14000).Redact(p => p.Secret)
            .ProjectAsync([p => p.Secret, p => p.Label], r => (Text(r, 0), r.GetString(1)));
        var scalar = await InBand(14000).Redact(p => p.Secret).ScalarAsync<string?>(p => p.Secret);
        var rows = await InBand(14000).Redact(p => p.Secret).ToListAsync();

        Assert.Equal(["s3cret"], open);
        Assert.Equal([(null, "L")], masked);
        Assert.Null(scalar);
        Assert.Null(Assert.Single(rows).Secret);
    }

    [SkippableFact(DisplayName = "ORML-14 B13 a conditional mask hides the column only in the rows that match")]
    public async Task A_conditional_mask_applies_to_a_projection()
    {
        await PrepareAsync();
        await SeedAsync(Probe(14100, "x", "a"), Probe(14100, "y", "b"));

        var seen = await InBand(14100).RedactWhen(p => p.Secret, p => p.Label == "x").OrderBy(p => p.Label)
            .ProjectAsync([p => p.Secret], r => Text(r, 0));

        Assert.Equal([null, "b"], seen);
    }

    [SkippableFact(DisplayName =
        "ORML-14 B14 SumAsync refuses a double column, AggregateSum keeps its value, an integer column sums")]
    public async Task SumAsync_does_not_round_a_double_sum()
    {
        await PrepareAsync();
        await SeedAsync(Probe(14200, "a", "x"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => InBand(14200).SumAsync(p => p.Weight));
        var row = await InBand(14200).AggregateSum(p => p.Weight).AggregatesAsync();

        Assert.Equal(0.4, Convert.ToDouble(row[0]), 9);
        Assert.Equal(14200L, await InBand(14200).SumAsync(p => p.Band));
    }

    [SkippableFact(DisplayName = "ORML-14 B15 an upsert Increment counts a NULL counter from zero")]
    public async Task An_upsert_increment_counts_a_null_counter_from_zero()
    {
        await PrepareAsync();
        var id = Guid.NewGuid();
        await SeedAsync(new QueryProbe { Id = id, Band = 14300, Tier = EnumTier.Low, Weight = 1 });
        var row = new Row<QueryProbe>().With(p => p.Id, id).With(p => p.Band, 14300)
            .With(p => p.Tier, EnumTier.Low).With(p => p.Weight, 1.0);
        UpsertSet<QueryProbe>[] sets = [UpsertSet<QueryProbe>.Increment(p => p.Hits, 2)];

        await Probes().UpsertAsync(row, [p => p.Id], OnConflict.DoUpdate, sets);
        await Probes().UpsertAsync(row, [p => p.Id], OnConflict.DoUpdate, sets);

        Assert.Equal(4, Assert.Single(await Probes().Where(p => p.Id == id).ToListAsync()).Hits);
    }

    [SkippableFact(DisplayName = "ORML-14 B16 a DoUpdate without a set is refused and changes no row")]
    public async Task A_do_update_without_a_set_is_refused()
    {
        await PrepareAsync();
        var id = Guid.NewGuid();
        await SeedAsync(new QueryProbe { Id = id, Band = 14400, Label = "kept", Tier = EnumTier.Low, Weight = 1 });
        var row = new Row<QueryProbe>().With(p => p.Id, id).With(p => p.Band, 14400)
            .With(p => p.Label, "lost").With(p => p.Tier, EnumTier.Low).With(p => p.Weight, 1.0);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Probes().UpsertAsync(row, [p => p.Id], OnConflict.DoUpdate));

        Assert.Equal("kept", Assert.Single(await Probes().Where(p => p.Id == id).ToListAsync()).Label);
    }
}
