using Xunit;
using Diff = global::Neemle.OrmLite.SchemaDiff;

namespace Neemle.OrmLite.Tests.SchemaDiff;

public sealed class WholeArtifactContractTests
{
    [Theory]
    [InlineData(0, SchemaDiffChangeKind.DropColumn)]
    [InlineData(1, SchemaDiffChangeKind.TypeChange)]
    [InlineData(2, SchemaDiffChangeKind.NullabilityChange)]
    [InlineData(3, SchemaDiffChangeKind.DefaultChange)]
    public void Individual_risks_block_the_entire_safe_create_proposal(int risk, SchemaDiffChangeKind expected)
    {
        var target = ContractFacts.Target(ContractFacts.Entity(), ContractFacts.Entity("safe_new"));
        var table = ChangedTable(risk);
        var report = Diff.Compare(target, ContractFacts.Observe(target, table, ContractFacts.Missing("safe_new")));
        Assert.Equal(SchemaDiffStatus.ManualRequired, report.Status);
        Assert.Contains(report.Changes, c => c.Kind == expected && c.Object.Table == "spec_items");
        Assert.Contains(report.Changes, c => c.Kind == SchemaDiffChangeKind.CreateTable);
        var error = Assert.Throws<SchemaDiffException>(() => Diff.Emit(report,
            new("0042_independent", "Contract.Generated", "Recipe")));
        Assert.Equal(SchemaDiffError.UnsupportedDifference, error.Error);
    }

    private static SchemaObservedTable ChangedTable(int risk)
    {
        var table = ContractFacts.Table();
        var note = ContractFacts.Column("note");
        note = risk switch
        {
            1 => note with { Type = SchemaFact<SchemaStorageType>.Known(new("integer", null, null)) },
            2 => note with { Nullable = SchemaFact<bool>.Known(false) },
            3 => note with { Facts = note.Facts with
                { Default = SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.EmptyText)) } },
            _ => note,
        };
        return table with { Columns = risk == 0
            ? [ContractFacts.Column("id", false), note, ContractFacts.Column("legacy")]
            : [ContractFacts.Column("id", false), note] };
    }

    [Fact(DisplayName = "[SEC][UC-011] Scope cannot adopt history or ambiguous case ownership")]
    public void Library_and_ambiguous_ownership_refuse()
    {
        foreach (var name in new[] { "schema_migrations", "row_history" })
        {
            var target = ContractFacts.Target(ContractFacts.Entity(name));
            Assert.Equal(SchemaDiffError.Ownership, Assert.Throws<SchemaDiffException>(() =>
                ContractFacts.Observe(target, ContractFacts.Missing(name))).Error);
        }
        var ambiguous = ContractFacts.Target(ContractFacts.Entity("Items"), ContractFacts.Entity("items"));
        Assert.Equal(SchemaDiffError.Ownership, Assert.Throws<SchemaDiffException>(() =>
            ContractFacts.Observe(ambiguous, ContractFacts.Missing("Items"), ContractFacts.Missing("items"))).Error);
    }
}
