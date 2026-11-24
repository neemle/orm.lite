using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeSources;
using static Neemle.Orm.Lite.Tests.EntityShapes.NamedShapeSources;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class RefusedNameGeneratorTests
{
    private const string Unreachable = "private, protected or private protected";

    [Theory(
        DisplayName = "B19 an entity the generated code cannot name gets one ORMLITE025 on the type, is not "
        + "generated, and leaves zero CS errors and a complete sibling")]
    [InlineData(PrivateNested, "Inner", Unreachable)]
    [InlineData(ProtectedNested, "Inner", Unreachable)]
    [InlineData(PrivateProtectedNested, "Inner", Unreachable)]
    [InlineData(InPrivateOuter, "Inner", Unreachable)]
    [InlineData(FileLocal, "Hidden", "file-local")]
    [InlineData(GenericOne, "Gen", "generic type")]
    [InlineData(GenericTwo, "Gen", "generic type")]
    [InlineData(GenericConstrained, "Gen", "generic type")]
    [InlineData(NestedInGeneric, "Inner", "generic type")]
    [InlineData(GenericView, "Gen", "generic type")]
    public void A_type_the_generated_code_cannot_name_is_refused_once(string source, string name, string reason)
    {
        var (diagnostics, generated, output) = Run(source + Sibling);

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(NameId, refusal.Id);
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Equal("OrmLite", refusal.Descriptor.Category);
        Assert.Equal(name, SpanText(refusal));
        Assert.Contains("'" + name + "'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains(reason, refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("not generated", refusal.GetMessage(), StringComparison.Ordinal);
        AssertPlainMessage(refusal.GetMessage());
        Assert.Empty(CompileErrors(output));
        Assert.DoesNotContain(name + "Orm", generated, StringComparison.Ordinal);
        Assert.Equal(["sibs"], GeneratedModel.Of(output).Schema.Entities.Select(e => e.Table).ToArray());
    }

    [Fact(
        DisplayName = "B19 a relation, a collection and a many-to-many list that name a generic entity lose only "
        + "their loaders")]
    public void A_referrer_of_a_generic_entity_loses_only_its_loaders()
    {
        var (diagnostics, _, output) = Run(GenericReferrer);

        Assert.Equal(NameId, Assert.Single(diagnostics).Id);
        Assert.Empty(CompileErrors(output));
        Assert.Equal(
            ["Column", "DeleteAsync", "GetAsync", "InsertAsync", "Map", "Query", "UpdateAsync", "Values"],
            Methods(GeneratedModel.Of(output), "HolderOrm"));
    }

    [Fact(DisplayName = "B19 a generic entity gives no ORMLITE001 for the property typed by its type parameter")]
    public void A_generic_entity_gives_no_unmapped_property_diagnostic()
    {
        var (diagnostics, _, _) = Run(GenericOne);

        Assert.DoesNotContain(diagnostics, d => d.Id == "ORMLITE001");
        Assert.Equal(NameId, Assert.Single(diagnostics).Id);
    }
}
