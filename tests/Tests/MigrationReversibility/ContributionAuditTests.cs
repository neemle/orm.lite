using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.MigrationReversibility;

public sealed class ContributionAuditTests
{
    private static readonly Node[] Invertible =
        [new CreateTable("ca_t", NodeList.Of([new ColumnDef("id", new TypeRef(ColumnType.Text), false, true)]))];

    private static readonly Node[] Lossy = [new DropTable("ca_t")];

    private static ISqlProvider ProviderOf(string id) =>
        id == "postgres" ? PostgresProvider.Instance : SqliteProvider.Instance;

    [Theory(DisplayName = "a hand-built entry is audited against its own operations: an honest declaration reports "
        + "nothing and a Reversible entry without an inverse or a Down, or an Irreversible one that can be inverted, "
        + "is reported")]
    [InlineData("postgres")]
    [InlineData("sqlite")]
    public void A_hand_built_entry_is_audited(string providerId)
    {
        var provider = ProviderOf(providerId);
        var honest = MigrationContribution.Of("1700_a", Reversibility.Reversible, Invertible);
        var lyingReversible = MigrationContribution.Of("1700_b", Reversibility.Reversible, Lossy);
        var withDown = MigrationContribution.Of("1700_c", Reversibility.Reversible, Lossy, b => b.Add(Invertible[0]));
        var lyingIrreversible = MigrationContribution.Of("1700_d", Reversibility.Irreversible, Invertible);

        Assert.Equal(ReversibilityFinding.None, MigrationOps.Audit(honest, provider));
        Assert.Equal(
            ReversibilityFinding.DeclaredReversibleButNotInvertible, MigrationOps.Audit(lyingReversible, provider));
        Assert.Equal(ReversibilityFinding.None, MigrationOps.Audit(withDown, provider));
        Assert.Equal(
            ReversibilityFinding.DeclaredIrreversibleButInvertible, MigrationOps.Audit(lyingIrreversible, provider));
    }

    [Fact(DisplayName = "an entry that carries no operations cannot be judged and reports nothing; an undefined "
        + "declaration is refused")]
    public void An_entry_without_nodes_reports_nothing()
    {
        var opaque = new MigrationContribution("1700_e", Reversibility.Reversible, _ => ["SELECT 1"], null);
        var undefined = opaque with { Reversibility = default };

        Assert.Equal(ReversibilityFinding.None, MigrationOps.Audit(opaque, SqliteProvider.Instance));
        Assert.Throws<InvalidOperationException>(() => MigrationOps.Audit(undefined, SqliteProvider.Instance));
    }
}
