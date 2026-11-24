using System.Globalization;
using Neemle.Orm.Lite.Nodes;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public abstract class RunValidationSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private OrmContext Ctx => fixture.Context;

    private string OtherProvider => fixture.ProviderId == "postgres" ? "sqlite" : "postgres";

    private static CreateTable Table(string name) =>
        new(name, NodeList.Of([new ColumnDef("id", new TypeRef(ColumnType.Text), false, true)]));

    private static MigrationContribution Of(string version, params Node[] operations) =>
        MigrationContribution.Of(version, Reversibility.Reversible, operations);

    private Task RunAsync(params MigrationContribution[] all) =>
        MigrationRunner.RunAsync(Ctx.DataSource, Ctx.Provider, all);

    private Task SeedAsync() => RunAsync(Of("2000_rv_seed", Table("rv_seed")));

    private async Task<string> StateAsync(string table)
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        var catalog = Ctx.Provider.CreateCatalog(connection);
        return await catalog.TableExistsAsync(table) ? "present" : "absent";
    }

    private async Task<string> HistoryAsync(string version)
    {
        var rows = await fixture.RowsAsync(
            $"SELECT count(*) FROM \"schema_migrations\" WHERE \"version\" = '{version}'");
        return Convert.ToString(rows[0][0], CultureInfo.InvariantCulture)!;
    }

    [SkippableFact(DisplayName = "a refusal in the second pending migration of a run is raised before the first is "
        + "committed, so the first leaves no table and no history row")]
    public async Task A_later_refusal_stops_the_earlier_migration()
    {
        fixture.RequireAvailable();
        await SeedAsync();
        var refused = Of("2011_rv_b", new RawSql(OtherProvider, "SELECT 1"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(Of("2010_rv_a", Table("rv_first")), refused));

        Assert.Equal("absent", await StateAsync("rv_first"));
        Assert.Equal("0", await HistoryAsync("2010_rv_a"));
    }

    [SkippableFact(DisplayName = "two contributions of one version are refused by name and neither of them runs, "
        + "where one of them used to run and the other to be skipped or to fail on the history key")]
    public async Task A_repeated_version_is_refused_and_neither_entry_runs()
    {
        fixture.RequireAvailable();
        await SeedAsync();

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(Of("2020_rv_d", Table("rv_one")), Of("2020_rv_d", Table("rv_two"))));

        Assert.Contains("'2020_rv_d'", refusal.Message, StringComparison.Ordinal);
        Assert.Equal("absent", await StateAsync("rv_one"));
        Assert.Equal("absent", await StateAsync("rv_two"));
        Assert.Equal("0", await HistoryAsync("2020_rv_d"));
    }

    [SkippableFact(DisplayName = "after a refused run the same migrations run once the refusal is removed, "
        + "each recorded once")]
    public async Task A_refused_run_does_not_block_a_later_valid_run()
    {
        fixture.RequireAvailable();
        await SeedAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(Of("2030_rv_a", Table("rv_later")), Of("2031_rv_b", new RawSql(OtherProvider, "SELECT 1"))));

        await RunAsync(Of("2030_rv_a", Table("rv_later")), Of("2031_rv_b", Table("rv_later_b")));

        Assert.Equal("present", await StateAsync("rv_later"));
        Assert.Equal("present", await StateAsync("rv_later_b"));
        Assert.Equal("1", await HistoryAsync("2030_rv_a"));
        Assert.Equal("1", await HistoryAsync("2031_rv_b"));
    }
}
