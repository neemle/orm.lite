using Neemle.OrmLite.Nodes;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.ForeignKeys;

public sealed class ForeignKeyGeneration
{
    internal static string Source(string reference = "", string inverse = "") => $$"""
        using Neemle.OrmLite.Nodes;
        [Table("z_parents")] public class Parent
        {
            public Guid Id { get; set; }
            {{inverse}} public System.Collections.Generic.List<Child> Children { get; set; } = new();
        }
        [Table("a_children")] public class Child
        {
            public Guid Id { get; set; }
            public Guid? ParentId { get; set; }
            {{reference}} public Parent? Parent { get; set; }
        }
        """;

    [Fact(DisplayName = "[UC-003] reference and inverse declarations normalize to one physical child-owned FK")]
    public void Inverse_and_reference_agree_on_the_physical_owner()
    {
        var attribute = "[ForeignKey(\"fk_child_parent\", ReferentialAction.SetNull)]";
        var (diagnostics, _, output) = Run(Source(attribute, attribute));
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        Assert.Empty(model.Descriptor("Parent").ForeignKeys);
        var key = Assert.Single(model.Descriptor("Child").ForeignKeys);
        Assert.Equal(new ForeignKeyDescriptor("fk_child_parent", "parent_id", new("z_parents", "id"),
            ReferentialAction.SetNull), key);
        Assert.Equal(["z_parents", "a_children"], model.Schema.Baseline().Cast<CreateTable>().Select(t => t.Table));
        Assert.Single(model.Schema.Baseline().Cast<CreateTable>().Last().Constraints!);
    }

    [Theory(DisplayName = "[SEC][UC-003] invalid generated intents withhold the complete schema class")]
    [InlineData("[ForeignKey(\"\", ReferentialAction.Cascade)]", "InvalidName")]
    [InlineData("[ForeignKey(\"fk\", (ReferentialAction)99)]", "InvalidDeleteAction")]
    [InlineData("[ForeignKey(\"fk\", ReferentialAction.Restrict)] [NotAColumn]", "UnknownRelation")]
    public void Invalid_declaration_has_location_and_category(string attribute, string category)
    {
        var (diagnostics, generated, output) = Run(Source(attribute));
        var diagnostic = Assert.Single(diagnostics, d => d.Id == "ORMLITE035");
        Assert.Contains(category, diagnostic.GetMessage());
        Assert.True(diagnostic.Location.IsInSource);
        Assert.DoesNotContain("class " + output.AssemblyName + "Schema", generated);
    }

    [Fact(DisplayName = "[SEC][UC-003] differing inverse actions fail rather than selecting a destructive policy")]
    public void Conflicting_inverse_action_is_diagnosed()
    {
        var (diagnostics, _, _) = Run(Source("[ForeignKey(\"fk\", ReferentialAction.Cascade)]",
            "[ForeignKey(\"fk\", ReferentialAction.Restrict)]"));
        Assert.Contains(diagnostics, d => d.Id == "ORMLITE035" && d.GetMessage().Contains("ConflictingDeclaration"));
    }
}
