using Microsoft.CodeAnalysis.CSharp;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Tests.IdentifierLimits;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.ForeignKeys.Final;

public sealed class FinalGeneratorTests
{
    [Theory(DisplayName = "[SEC][UC-003] one pure-link opt-in never installs the other end's constraint or action")]
    [InlineData(LinkForeignKeyEnd.Source)]
    [InlineData(LinkForeignKeyEnd.Target)]
    public void One_link_end_is_exactly_one_physical_constraint(LinkForeignKeyEnd end)
    {
        var plan = FinalLinkWitness.Model(end).Schema.Baseline().Cast<CreateTable>().ToArray();
        var expected = end == LinkForeignKeyEnd.Source
            ? new[] { "final_roles", "final_users", "final_links" }
            : ["final_roles", "final_links", "final_users"];
        Assert.Equal(expected, plan.Select(t => t.Table));
        var join = Assert.Single(plan, table => table.Table == "final_links");
        var key = Assert.IsType<ForeignKeyDef>(Assert.Single(join.Constraints!));
        Assert.Equal(end == LinkForeignKeyEnd.Source ? "final_users" : "final_roles", key.ReferencedTable);
        Assert.Equal(ReferentialAction.Cascade, key.OnDelete);
        Assert.Null(key.OnUpdate);
        Assert.All(plan.Where(table => table.Table != "final_links"), table => Assert.Empty(table.Constraints!));
    }

    [Fact(DisplayName = "[UC-003] a generated owner's unrelated integer key does not constrain its Guid FK")]
    public void Ordinary_owner_key_is_not_mistaken_for_the_referenced_target_key()
    {
        var source = ForeignKeyGeneration.Source("[ForeignKey(\"fk_guid_target\", ReferentialAction.Restrict)]");
        var start = source.IndexOf("[Table(\"a_children\")]", StringComparison.Ordinal);
        source = source[..start] + source[start..].Replace("Guid Id", "int Id");
        var (diagnostics, _, output) = Run(source);
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        var key = Assert.Single(model.Descriptor("Child").ForeignKeys);
        Assert.Equal("parent_id", key.LocalColumn);
        Assert.Equal(new ForeignKeyTarget("z_parents", "id"), key.Target);
        Assert.Equal(["z_parents", "a_children"], model.Schema.Baseline().Cast<CreateTable>().Select(t => t.Table));
    }

    [Theory(DisplayName = "[SEC][UC-003] provider identifier bounds count UTF8 bytes instead of Unicode characters")]
    [InlineData(15, false)]
    [InlineData(16, true)]
    public void Quoted_unicode_constraint_names_obey_the_provider_byte_boundary(int glyphs, bool oversized)
    {
        var name = "fk_" + string.Concat(Enumerable.Repeat("🚀", glyphs));
        var source = ForeignKeyGeneration.Source("[ForeignKey(" + SymbolDisplay.FormatLiteral(name, true)
            + ", ReferentialAction.Restrict)]");
        var diagnostics = ManifestHarness.Run(source, "postgres", ManifestHarness.Manifest("63"));
        if (oversized) Assert.Contains(diagnostics, issue => issue.GetMessage().Contains("constraint"));
        else Assert.Empty(diagnostics);
    }
}
