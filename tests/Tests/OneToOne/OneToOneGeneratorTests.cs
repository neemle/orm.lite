using Neemle.OrmLite.Nodes;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.OneToOne;

[Collection("Graph")]
public sealed class OneToOneGeneratorTests
{
    [Theory(DisplayName = "[UC-005][SEC] invalid pair declarations withhold the complete baseline")]
    [InlineData("nameof(Child.Parent)", "\"Missing\"", "InvalidInverse")]
    [InlineData("public Parent? Parent { get; set; }", "public Parent? Parent { get; init; }", "InvalidNullability")]
    [InlineData("public Parent? Parent", "public Parent Parent", "InvalidNullability")]
    [InlineData("[ForeignKey(\"fk_sdet_oto\", ReferentialAction.Restrict)]", "", "MissingConstraintIntent")]
    [InlineData("[OneToOneUnique(\"uq_sdet_oto\")]", "", "MissingConstraintIntent")]
    [InlineData("uq_sdet_oto", "fk_sdet_oto", "ConflictingConstraint")]
    [InlineData("public int? ParentId", "public long? ParentId", "IncompatibleKey")]
    [InlineData("public int? ParentId", "[NotAColumn] public int? ParentId", "UnmappedForeignKey")]
    public void Invalid_pair_has_category_and_source_location(string original, string replacement, string category)
    {
        var (diagnostics, generated, output) = Run(OneToOneSource.Text.Replace(original, replacement));
        Assert.Contains(diagnostics, d => d.Id == "ORMLITE036" && d.GetMessage().Contains(category)
            && d.Location.IsInSource);
        Assert.DoesNotContain("class " + output.AssemblyName + "Schema", generated);
    }

    [Fact(DisplayName = "[UC-005] generated pair has one exact baseline-only unique index and typed registration")]
    public void Exact_unique_is_owned_by_the_baseline()
    {
        var (diagnostics, _, output) = Run(OneToOneSource.Text);
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        var child = model.Descriptor("Child");
        var relation = Assert.Single(child.OneToOnes);
        Assert.Equal("parent_id", relation.Owner.ForeignKeyColumn);
        Assert.Empty(model.Descriptor("Parent").OneToOnes);
        var plan = model.Schema.Baseline();
        Assert.Equal(new[] { "sdet_oto_parent", "sdet_oto_child" },
            plan.OfType<CreateTable>().Select(t => t.Table));
        var unique = Assert.IsType<CreateIndex>(plan.Last());
        Assert.Equal("uq_sdet_oto", unique.Index);
        Assert.True(unique.Unique);
        Assert.Equal("parent_id", Assert.IsType<ColumnRef>(Assert.Single(unique.Terms)).Column);
        Assert.Empty(child.Indexes);
        Assert.Empty(model.Schema.Indexes());
        model.RunModuleInitializer();
    }
}
