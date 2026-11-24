using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

// ORML-13 (B12): a group key over an enum column is read as the enum value on both providers. The driver's own
// GetFieldValue<TEnum> does not read the stored member name (Npgsql refuses it, Microsoft.Data.Sqlite turns the text
// into 0), so before this the key was an exception on one provider and the wrong member on the other.
public abstract class GroupEnumKeySuite<TFixture>(TFixture fixture) : QueryProbeSuite<TFixture>(fixture)
    where TFixture : ProviderFixture
{
    private static QueryProbe Probe(int band, EnumTier tier, EnumTier? alt) =>
        new() { Band = band, Tier = tier, Alt = alt, Weight = 1 };

    private Task<int> SeedTiersAsync(int band) => SeedAsync(
        Probe(band, EnumTier.Low, null),
        Probe(band, EnumTier.Low, EnumTier.High),
        Probe(band, EnumTier.Low, EnumTier.High),
        Probe(band, EnumTier.Mid, EnumTier.Mid),
        Probe(band, EnumTier.Mid, null),
        Probe(band, EnumTier.High, EnumTier.High));

    [SkippableFact(DisplayName = "ORML-13 a group key over an enum column is read as the enum value")]
    public async Task An_enum_key_is_read_as_the_enum_value()
    {
        await PrepareAsync();
        await SeedTiersAsync(13000);

        var grouping = Probes().Where(p => p.Band == 13000).GroupBy(p => p.Tier);
        var count = grouping.Count();
        var rows = await grouping.OrderBy(count, descending: true).ToListAsync();

        Assert.Equal([EnumTier.Low, EnumTier.Mid, EnumTier.High], rows.Select(r => r.Key));
        Assert.Equal([3L, 2L, 1L], rows.Select(r => r.Get(count)));
    }

    [SkippableFact(DisplayName =
        "ORML-13 a nullable enum key is read as null for the NULL group and as the member otherwise")]
    public async Task A_nullable_enum_key_reads_null_for_the_null_group()
    {
        await PrepareAsync();
        await SeedTiersAsync(13100);

        var grouping = Probes().Where(p => p.Band == 13100).GroupBy(p => p.Alt);
        var count = grouping.Count();
        var rows = await grouping.OrderBy(count, descending: true).ToListAsync();

        Assert.Equal([EnumTier.High, null, EnumTier.Mid], rows.Select(r => r.Key));
        Assert.Equal([3L, 2L, 1L], rows.Select(r => r.Get(count)));
    }

    [SkippableFact(DisplayName = "ORML-13 a two-key grouping reads an enum key and a nullable enum key together")]
    public async Task A_tuple_key_reads_each_enum_member()
    {
        await PrepareAsync();
        await SeedTiersAsync(13200);

        var grouping = Probes().Where(p => p.Band == 13200).GroupBy(p => p.Tier, p => p.Alt);
        var count = grouping.Count();
        var rows = await grouping.OrderBy(count, descending: true).ToListAsync();

        Assert.Contains(rows, r => r.Key == (EnumTier.Low, EnumTier.High) && r.Get(count) == 2);
        Assert.Contains(rows, r => r.Key == (EnumTier.Low, null) && r.Get(count) == 1);
        Assert.Equal(5, rows.Count);
    }

    [SkippableFact(DisplayName =
        "ORML-13 a stored name that is not a member raises an OrmEnumValueException for the key")]
    public async Task An_unknown_stored_name_is_reported_by_table_and_column()
    {
        await PrepareAsync();
        await Fixture.ExecuteAsync(
            "INSERT INTO \"query_probe_rows\" (\"id\", \"band\", \"tier\", \"weight\") "
            + $"VALUES ('{Guid.NewGuid()}', 13300, 'Bogus', 1)");

        var grouping = Probes().Where(p => p.Band == 13300).GroupBy(p => p.Tier);
        var failure = await Assert.ThrowsAsync<OrmEnumValueException>(() => grouping.ToListAsync());

        Assert.Equal(("query_probe_rows", "tier", "Bogus"), (failure.Table, failure.Column, failure.Value));
        Assert.Equal(["Low", "Mid", "High"], failure.Accepted);
    }
}
