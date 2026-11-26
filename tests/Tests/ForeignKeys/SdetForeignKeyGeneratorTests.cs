using Neemle.OrmLite.Nodes;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.ForeignKeys;

public sealed class SdetForeignKeyGeneratorTests
{
    [Theory(DisplayName = "[UC-003] inverse-only opt-in keeps the physical child owner and exact declared action")]
    [InlineData(ReferentialAction.NoAction)]
    [InlineData(ReferentialAction.Restrict)]
    [InlineData(ReferentialAction.Cascade)]
    [InlineData(ReferentialAction.SetNull)]
    public void Inverse_only_metadata_has_exact_physical_meaning(ReferentialAction action)
    {
        var attribute = "[ForeignKey(\"fk_inverse\", ReferentialAction." + action + ")]";
        var (diagnostics, _, output) = Run(ForeignKeyGeneration.Source(inverse: attribute));
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        Assert.Empty(model.Descriptor("Parent").ForeignKeys);
        var key = Assert.Single(model.Descriptor("Child").ForeignKeys);
        Assert.Equal(new ForeignKeyDescriptor("fk_inverse", "parent_id", new("z_parents", "id"), action), key);
        Assert.Single(model.Schema.Baseline().OfType<CreateTable>().Last().Constraints!);
    }

    [Theory(DisplayName = "[SEC][UC-003] incompatible numeric precision or scale withholds the generated baseline")]
    [InlineData(13, 4)]
    [InlineData(12, 3)]
    public void Decimal_shape_mismatch_is_not_erased_by_the_underlying_clr_type(int precision, int scale)
    {
        var source = ForeignKeyGeneration.Source("[ForeignKey(\"fk\", ReferentialAction.Restrict)]")
            .Replace("Guid Id", "decimal Id")
            .Replace("public Guid? ParentId", $"[Numeric({precision},{scale})] public decimal? ParentId");
        var (diagnostics, generated, output) = Run(source);
        Assert.Contains(diagnostics, d => d.Id == "ORMLITE035" && d.GetMessage().Contains("IncompatibleKey"));
        Assert.DoesNotContain("class " + output.AssemblyName + "Schema", generated);
    }

    [Fact(DisplayName = "[SEC][UC-003] a same-named unrelated attribute does not install foreign-key intent")]
    public void Attribute_namespace_is_part_of_the_opt_in_contract()
    {
        var source = ForeignKeyGeneration.Source("[Other.ForeignKey]") + "\n" + """
            namespace Other { public sealed class ForeignKeyAttribute : System.Attribute { } }
            """;
        var (diagnostics, _, output) = Run(source);
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        Assert.Empty(model.Descriptor("Child").ForeignKeys);
        Assert.All(model.Schema.Baseline().OfType<CreateTable>(), table => Assert.Null(table.Constraints));
    }

    [Fact(DisplayName = "[UC-003] an explicit Reference property selects only its mapped local FK column")]
    public void Explicit_reference_is_not_replaced_by_a_navigation_name_guess()
    {
        var source = ForeignKeyGeneration.Source(
                "[Reference(\"OwnerId\")] [ForeignKey(\"fk_explicit\", ReferentialAction.NoAction)]")
            .Replace("Guid? ParentId", "Guid? OwnerId")
            .Replace("public System.Collections.Generic.List<Child>",
                "[Reference(\"OwnerId\")] public System.Collections.Generic.List<Child>");
        var (diagnostics, _, output) = Run(source);
        Assert.Empty(diagnostics);
        Assert.Equal("owner_id", Assert.Single(GeneratedModel.Of(output).Descriptor("Child").ForeignKeys).LocalColumn);
    }
}
