using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.MigrationReversibility;

public sealed class IrreversibleRevertRefusalTests
{
    private const string Version = "0200_irreversible_refusal";

    private static ISqlProvider ProviderOf(string id) =>
        id == "postgres" ? PostgresProvider.Instance : SqliteProvider.Instance;

    private static Node[] InvertibleUp() =>
    [
        new CreateTable("mr_t", NodeList.Of([new ColumnDef("id", new TypeRef(ColumnType.Text), false, true)])),
        new AddColumn("mr_t", new ColumnDef("note", new TypeRef(ColumnType.Text))),
    ];

    private static async Task<InvalidOperationException> RefusedAsync(
        string providerId, MigrationContribution contribution) =>
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => MigrationRunner.RevertAsync(new NeverUsedDataSource(), ProviderOf(providerId), contribution));

    private static void AssertRefusalNamesVersionIrreversibleAndNothingExecuted(Exception refusal)
    {
        Assert.Contains(Version, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("Irreversible", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("nothing was executed", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "an Irreversible migration with an invertible Up is refused by name "
        + "before the data source, a lock or a transaction is touched")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public async Task An_invertible_irreversible_migration_is_refused_before_any_connection(string providerId)
    {
        var contribution = MigrationContribution.Of(Version, Reversibility.Irreversible, InvertibleUp());

        var refusal = await RefusedAsync(providerId, contribution);

        AssertRefusalNamesVersionIrreversibleAndNothingExecuted(refusal);
    }

    [Theory(DisplayName = "an Irreversible migration whose Up cannot be inverted is refused as Irreversible, "
        + "not as having no derivable Down")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public async Task A_lossy_irreversible_migration_is_refused_as_irreversible(string providerId)
    {
        var contribution = MigrationContribution.Of(
            Version, Reversibility.Irreversible, [new DropColumn("mr_t", "note")]);

        var refusal = await RefusedAsync(providerId, contribution);

        AssertRefusalNamesVersionIrreversibleAndNothingExecuted(refusal);
        Assert.DoesNotContain("none can be derived", refusal.Message, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "an Irreversible migration that carries a Down is refused "
        + "without the Down being invoked")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public async Task A_carried_down_is_never_invoked(string providerId)
    {
        var invoked = 0;
        var contribution = MigrationContribution.Of(
            Version, Reversibility.Irreversible, InvertibleUp(), b => { invoked++; b.DropTable("mr_t"); });

        var refusal = await RefusedAsync(providerId, contribution);

        AssertRefusalNamesVersionIrreversibleAndNothingExecuted(refusal);
        Assert.Equal(0, invoked);
    }

    [Theory(DisplayName = "a hand-built Irreversible contribution with a Down and no Nodes is refused "
        + "without the Down being invoked")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public async Task A_hand_built_contribution_without_nodes_is_refused_without_invoking_its_down(string providerId)
    {
        var invoked = 0;
        var contribution = new MigrationContribution(
            Version, Reversibility.Irreversible, _ => [], b => { invoked++; });

        Assert.Null(contribution.Nodes);
        var refusal = await RefusedAsync(providerId, contribution);

        AssertRefusalNamesVersionIrreversibleAndNothingExecuted(refusal);
        Assert.Equal(0, invoked);
    }
}
