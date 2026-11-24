using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Nodes;

public sealed class MigrationRunnerDuplicateVersionTests
{
    private static ISqlProvider ProviderOf(string id) =>
        id == "postgres" ? PostgresProvider.Instance : SqliteProvider.Instance;

    private static MigrationContribution Of(string version, string table) =>
        MigrationContribution.Of(
            version,
            Reversibility.Reversible,
            [new CreateTable(table, NodeList.Of([new ColumnDef("id", new TypeRef(ColumnType.Text), false, true)]))]);

    private static Task RunAsync(string providerId, params MigrationContribution[] all) =>
        MigrationRunner.RunAsync(new NeverUsedDataSource(), ProviderOf(providerId), all);

    [Theory(DisplayName = "two contributions of one version are refused by name as InvalidOperationException "
        + "before the data source is touched")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public async Task A_repeated_version_is_refused_before_any_connection(string providerId)
    {
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(providerId, Of("0100_x", "dv_a"), Of("0100_x", "dv_b")));

        Assert.Contains("'0100_x'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("more than once", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "a repeated version is refused whatever the supplied order and whatever lies between "
        + "the two entries, and neither of them is rendered")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public async Task A_repeated_version_is_found_in_any_supplied_order(string providerId)
    {
        var renders = 0;
        var counting = new MigrationContribution(
            "0100_x", Reversibility.Reversible, _ => { renders++; return ["SELECT 1"]; }, null);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(providerId, counting, Of("0050_y", "dv_y"), counting, Of("0200_z", "dv_z")));

        Assert.Contains("'0100_x'", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(0, renders);
    }

    [Theory(DisplayName = "versions that differ only in case are two versions, as the history table compares them, "
        + "so the run proceeds to the data source")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public async Task Versions_are_compared_ordinally(string providerId)
    {
        await Assert.ThrowsAsync<NotSupportedException>(
            () => RunAsync(providerId, Of("0100_x", "dv_a"), Of("0100_X", "dv_b")));
    }
}
