using Neemle.Orm.Lite;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.MigrationReversibility;

public sealed class GeneratedContributionDeclarationTests
{
    private static IReadOnlyList<MigrationContribution> Contributions() => GeneratedContributions(Run(@"
[Migration(""0052_drop_workitem_slim_columns"", Reversibility.Irreversible)]
public sealed class DropSlim : Migration
{
    public override void Up(MigrationBuilder b) => b.Raw(""postgres"", ""ALTER TABLE t DROP COLUMN c"");
}

[Migration(""0058_add_workitem_summary_column"", Reversibility.Reversible)]
public sealed class AddSummary : Migration
{
    public override void Up(MigrationBuilder b) => b.Raw(""postgres"", ""ALTER TABLE t ADD COLUMN c text"");
}").Output);

    [Fact(DisplayName = "the compiled Contributions carry each migration's declared value, read from the generated assembly")]
    public void Each_declared_value_reaches_the_generated_entry()
    {
        var contributions = Contributions();

        Assert.Equal(Reversibility.Irreversible, Assert.Single(contributions, c => c.Version == "0052_drop_workitem_slim_columns").Reversibility);
        Assert.Equal(Reversibility.Reversible, Assert.Single(contributions, c => c.Version == "0058_add_workitem_summary_column").Reversibility);
    }

    [Fact(DisplayName = "of the three generated entries per assembly the indexes are Reversible and the baseline "
        + "and the reconcile are Irreversible")]
    public void The_baseline_and_reconcile_are_irreversible_and_the_indexes_are_reversible()
    {
        var contributions = Contributions();

        var generated = contributions
            .Where(c => c.Version.StartsWith("0000_baseline_", StringComparison.Ordinal)
                     || c.Version.StartsWith("0001_orm_indexes_", StringComparison.Ordinal)
                     || c.Version.StartsWith("9900_orm_index_reconcile_", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(3, generated.Count);
        var reconcile = Assert.Single(generated, c => c.Version.StartsWith("9900_", StringComparison.Ordinal));
        var baseline = Assert.Single(generated, c => c.Version.StartsWith("0000_baseline_", StringComparison.Ordinal));
        var indexes = Assert.Single(generated, c => c.Version.StartsWith("0001_orm_", StringComparison.Ordinal));
        Assert.Equal(Reversibility.Irreversible, reconcile.Reversibility);
        Assert.Equal(Reversibility.Irreversible, baseline.Reversibility);
        Assert.Equal(Reversibility.Reversible, indexes.Reversibility);
    }
}
