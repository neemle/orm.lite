using Xunit;
using Diff = global::Neemle.OrmLite.SchemaDiff;

namespace Neemle.OrmLite.Tests.SchemaDiff;

public sealed class UnknownContractTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Every_unknown_column_fact_blocks_equality_and_emission(int fact)
    {
        var target = ContractFacts.Target(ContractFacts.Entity());
        var table = ContractFacts.Table();
        var column = UnknownColumn(ContractFacts.Column("note"), fact);
        var report = Diff.Compare(target, ContractFacts.Observe(target,
            table with { Columns = [ContractFacts.Column("id", false), column] }));
        Assert.Equal(SchemaDiffStatus.Incomplete, report.Status);
        Assert.Contains(report.Changes, c => c.Kind == SchemaDiffChangeKind.UnknownMetadata
            && c.Object.Column == "note");
        Assert.Throws<SchemaDiffException>(() => Diff.Emit(report, new("0042_test", "Tests", "Recipe")));
    }

    private static SchemaObservedColumn UnknownColumn(SchemaObservedColumn column, int fact) => fact switch
    {
        0 => column with { Type = SchemaFact<SchemaStorageType>.Unknown("not-observed") },
        1 => column with { Nullable = SchemaFact<bool>.Unknown("not-observed") },
        2 => column with { KeyPosition = SchemaFact<int>.Unknown("not-observed") },
        3 => column with { Facts = column.Facts with { Default = SchemaFact<SchemaDefault>.Unknown("missing") } },
        4 => column with { Facts = column.Facts with { Generated = SchemaFact<bool>.Unknown("missing") } },
        5 => column with { Facts = column.Facts with { Identity = SchemaFact<bool>.Unknown("missing") } },
        _ => column with { Facts = column.Facts with { Collation = SchemaFact<string?>.Unknown("missing") } },
    };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Every_unknown_inventory_blocks_a_safe_create_prefix(int fact)
    {
        var target = ContractFacts.Target(ContractFacts.Entity(), ContractFacts.Entity("safe_new"));
        var unknown = SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Unknown("unobserved-inventory");
        var inventory = ContractFacts.Inventory();
        inventory = fact switch
        {
            0 => inventory with { Indexes = unknown },
            1 => inventory with { Constraints = unknown },
            2 => inventory with { Triggers = unknown },
            _ => inventory with { DependentObjects = unknown },
        };
        var report = Diff.Compare(target, ContractFacts.Observe(target,
            ContractFacts.Table() with { Facts = inventory }, ContractFacts.Missing("safe_new")));
        Assert.Equal(SchemaDiffStatus.Incomplete, report.Status);
        Assert.Throws<SchemaDiffException>(() => Diff.Emit(report, new("0042_test", "Tests", "Recipe")));
    }

    [Fact]
    public void Complete_known_equality_is_noop_but_unknown_existence_is_not_absence()
    {
        var target = ContractFacts.Target(ContractFacts.Entity());
        var equal = Diff.Compare(target, ContractFacts.Observe(target, ContractFacts.Table()));
        Assert.Equal(SchemaDiffStatus.NoOp, equal.Status);
        Assert.Empty(equal.Changes);
        var unknown = ContractFacts.Missing("spec_items") with { Exists = SchemaFact<bool>.Unknown("unknown") };
        Assert.Equal(SchemaDiffStatus.Incomplete, Diff.Compare(target, ContractFacts.Observe(target, unknown)).Status);
        Assert.Throws<SchemaDiffException>(() => SchemaFact<bool>.Unknown("unknown").GetKnown());
        Assert.Throws<SchemaDiffException>(() => Diff.Emit(equal, new("0042_test", "Tests", "Recipe")));
    }
}
