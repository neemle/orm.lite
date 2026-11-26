using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Tests.IdentifierLimits;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.ForeignKeys;

public sealed class ForeignKeyShapeTests
{
    [Theory(DisplayName = "[SEC][UC-003] unsupported generated endpoint/key shapes have explicit categories")]
    [InlineData("type", "IncompatibleKey")]
    [InlineData("unmanaged", "UnmanagedEndpoint")]
    [InlineData("column", "UnmappedColumn")]
    [InlineData("key", "UnsupportedKey")]
    public void Invalid_endpoint_withholds_baseline(string change, string category)
    {
        var source = ForeignKeyGeneration.Source("[ForeignKey(\"fk\", ReferentialAction.Restrict)]");
        source = change switch
        {
            "type" => source.Replace("Guid? ParentId", "int? ParentId"),
            "unmanaged" => source.Replace("[Table(\"z_parents\")]", "[Table(\"z_parents\", Managed = false)]"),
            "column" => source.Replace("public Guid? ParentId", "[NotAColumn] public Guid? ParentId"),
            _ => source.Replace("public Guid Id", "public Guid? Id"),
        };
        var (diagnostics, generated, output) = Run(source);
        Assert.Contains(diagnostics, d => d.Id == "ORMLITE035" && d.GetMessage().Contains(category));
        Assert.DoesNotContain("class " + output.AssemblyName + "Schema", generated);
    }

    [Fact(DisplayName = "[UC-003] decimal keys compare effective precision and scale")]
    public void Equivalent_default_numeric_shapes_are_compatible()
    {
        var source = ForeignKeyGeneration.Source("[ForeignKey(\"fk\", ReferentialAction.Restrict)]")
            .Replace("public Guid Id", "public decimal Id")
            .Replace("public Guid? ParentId", "[Numeric(12,4)] public decimal? ParentId");
        var (diagnostics, _, output) = Run(source);
        Assert.Empty(diagnostics);
        Assert.Single(GeneratedModel.Of(output).Descriptor("Child").ForeignKeys);
    }

    [Fact(DisplayName = "[SEC][UC-003] declared FK identifier limits remain provider-owned in generator and DDL")]
    public void Provider_limit_rejects_oversized_constraint_before_execution()
    {
        var name = new string('x', 64);
        var source = ForeignKeyGeneration.Source("[ForeignKey(\"" + name + "\", ReferentialAction.Restrict)]");
        var diagnostics = ManifestHarness.Run(source, "postgres", ManifestHarness.Manifest("63"));
        Assert.Contains(diagnostics, d => d.GetMessage().Contains("constraint"));
        var schema = OrmSchema.Of("Long", ForeignKeyPlans.Child().ForeignKey(ForeignKeyPlans.Key(name)),
            ForeignKeyPlans.Parent());
        Assert.Throws<IdentifierTooLongException>(
            () => DdlCompiler.Render(schema.Baseline(), PostgresProvider.Instance));
    }
    [Fact(DisplayName = "[SEC][UC-003] malformed UTF16 constraint names refuse instead of being rewritten by encoding")]
    public void Invalid_unicode_is_not_an_identifier_alias()
    {
        var name = "fk_" + (char)0xd800;
        var schema = OrmSchema.Of("Unicode", ForeignKeyPlans.Child().ForeignKey(ForeignKeyPlans.Key(name)),
            ForeignKeyPlans.Parent());
        Assert.Equal(ForeignKeyDeclarationError.InvalidName,
            Assert.Throws<ForeignKeyDeclarationException>(() => schema.Baseline()).Error);
        var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(name, true);
        var (diagnostics, _, _) = Run(ForeignKeyGeneration.Source(
            "[ForeignKey(" + literal + ", ReferentialAction.Restrict)]"));
        Assert.Contains(diagnostics, d => d.Id == "ORMLITE035" && d.GetMessage().Contains("InvalidName"));
    }

    [Fact(DisplayName = "[UC-003] invalid manual target metadata cannot admit unsupported physical key shapes")]
    public void Manual_metadata_refuses_null_targets_and_composite_owners()
    {
        var nullKey = ForeignKeyPlans.Key() with { Target = null! };
        var schema = OrmSchema.Of("Null", ForeignKeyPlans.Child().ForeignKey(nullKey), ForeignKeyPlans.Parent());
        Assert.Equal(ForeignKeyDeclarationError.UnsupportedKey,
            Assert.Throws<ForeignKeyDeclarationException>(() => schema.Baseline()).Error);
        var composite = ForeignKeyPlans.Child().Property("Part", "part", ColumnType.Uuid, false, true)
            .ForeignKey(ForeignKeyPlans.Key());
        Assert.Equal(ForeignKeyDeclarationError.UnsupportedKey, Assert.Throws<ForeignKeyDeclarationException>(
            () => OrmSchema.Of("Composite", composite, ForeignKeyPlans.Parent()).Baseline()).Error);
    }
    [Fact(DisplayName = "[UC-003] complete opted-in plans reject unsupported unrelated managed entity keys")]
    public void Invalid_real_entity_prevents_partial_admission()
    {
        var invalid = EntityDescriptor.Of("Bad", "bad", true)
            .Property("One", "one", ColumnType.Uuid, false, true)
            .Property("Two", "two", ColumnType.Uuid, false, true);
        var schema = OrmSchema.Of("All", invalid, ForeignKeyPlans.Child().ForeignKey(ForeignKeyPlans.Key()),
            ForeignKeyPlans.Parent());
        Assert.Equal(ForeignKeyDeclarationError.UnsupportedKey,
            Assert.Throws<ForeignKeyDeclarationException>(() => schema.Baseline()).Error);
    }

    [Fact(DisplayName = "[SEC][UC-003] an unrelated view is not created or altered by an opted-in baseline")]
    public void View_metadata_does_not_acquire_table_ownership()
    {
        var view = EntityDescriptor.Of("View", "view_only", true).View();
        var schema = OrmSchema.Of("Views", view, ForeignKeyPlans.Child().ForeignKey(ForeignKeyPlans.Key()),
            ForeignKeyPlans.Parent());
        Assert.DoesNotContain(schema.Baseline().OfType<CreateTable>(), t => t.Table == "view_only");
    }
    [Fact(DisplayName = "[SEC][UC-003] invalid FK identifiers are not echoed by provider-length diagnostics")]
    public void Invalid_constraint_name_never_enters_the_length_diagnostic()
    {
        var name = new string('x', 70) + "\nprivate-value";
        var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(name, true);
        var source = ForeignKeyGeneration.Source("[ForeignKey(" + literal + ", ReferentialAction.Restrict)]");
        Assert.Empty(ManifestHarness.Run(source, "postgres", ManifestHarness.Manifest("63")));
        var (diagnostics, _, _) = Run(source);
        var issue = Assert.Single(diagnostics, d => d.Id == "ORMLITE035");
        Assert.Contains("InvalidName", issue.GetMessage());
        Assert.DoesNotContain("private-value", issue.GetMessage());
    }
}
