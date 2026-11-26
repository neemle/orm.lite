using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.ForeignKeys.Sdet;

public sealed class SdetDependencyGeneratorTests
{
    [Theory(DisplayName = "[SEC][UC-003] generated self and cyclic FK graphs withhold the entire baseline")]
    [InlineData(false)]
    [InlineData(true)]
    public void Unsupported_cycles_are_diagnosed_before_any_schema_execution(bool cycle)
    {
        var (diagnostics, generated, output) = Run(CyclicSource(cycle));
        Assert.Contains(diagnostics,
            issue => issue.Id == "ORMLITE035" && issue.GetMessage().Contains("DependencyCycle"));
        Assert.DoesNotContain("class " + output.AssemblyName + "Schema", generated);
    }

    [Fact(DisplayName = "[UC-003] reordered and repeated model discovery preserves physical FK order and identity")]
    public void Discovery_order_cannot_change_the_semantic_baseline()
    {
        var source = ForeignKeyGeneration.Source("[ForeignKey(\"fk_discovery\", ReferentialAction.Restrict)]");
        var start = source.IndexOf("[Table(\"a_children\")]", StringComparison.Ordinal);
        var parent = source[..start].Replace("using Neemle.OrmLite.Nodes;", "");
        var reverse = "using Neemle.OrmLite.Nodes;\n" + source[start..] + parent;
        var first = Plan(source);
        Assert.Equal(first, Plan(reverse));
        Assert.Equal(first, Plan(source));
        Assert.Equal(2, first.Count);
        Assert.Contains("z_parents", first[0]);
        Assert.Contains("fk_discovery", first[1]);
    }

    private static IReadOnlyList<string> Plan(string source)
    {
        var (diagnostics, _, output) = Run(source);
        Assert.Empty(diagnostics);
        return GeneratedModel.Render(GeneratedModel.Of(output).Schema.Baseline());
    }

    private static string CyclicSource(bool cycle)
    {
        if (!cycle) return """
            using Neemle.OrmLite.Nodes;
            [Table("self_nodes")] public class Node {
                public Guid Id { get; set; } public Guid? ParentId { get; set; }
                [ForeignKey("fk_self", ReferentialAction.Restrict)] public Node? Parent { get; set; }
            }
            """;
        return """
            using Neemle.OrmLite.Nodes;
            [Table("cycle_a")] public class A {
                public Guid Id { get; set; } public Guid? BId { get; set; }
                [ForeignKey("fk_a_b", ReferentialAction.Restrict)] public B? B { get; set; }
            }
            [Table("cycle_b")] public class B {
                public Guid Id { get; set; } public Guid? AId { get; set; }
                [ForeignKey("fk_b_a", ReferentialAction.Cascade)] public A? A { get; set; }
            }
            """;
    }
}
