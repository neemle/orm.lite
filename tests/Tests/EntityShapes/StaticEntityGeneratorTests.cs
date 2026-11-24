using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.OrmLite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.OrmLite.Tests.EntityShapes.EntityShapeSources;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.EntityShapes;

public sealed class StaticEntityGeneratorTests
{
    private static readonly string[] SignatureErrors = ["CS0718", "CS0721", "CS0722", "CS0723"];

    [Fact(
        DisplayName = "AC19 a static [Table] class gets exactly one T, is not generated, and its sibling is "
        + "complete")]
    public void A_static_entity_is_refused_and_dropped()
    {
        var (diagnostics, generated, output) = Run(StaticEntity + Sibling);

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(TypeId, refusal.Id);
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Equal("OrmLite", refusal.Descriptor.Category);
        Assert.Equal("SE", SpanText(refusal));
        Assert.Contains("'SE'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("static", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("not generated", refusal.GetMessage(), StringComparison.Ordinal);
        var model = GeneratedModel.Of(output);
        Assert.False(model.Has("SEOrm"));
        Assert.False(model.Has("SEPurge"));
        Assert.DoesNotContain("SEOrm", generated, StringComparison.Ordinal);
        Assert.Equal(["sibs"], model.Schema.Entities.Select(e => e.Table).ToArray());
        Assert.Equal(
            ["Column", "DeleteAsync", "GetAsync", "InsertAsync", "Map", "Query", "UpdateAsync", "Values"],
            Methods(model, "SibOrm"));
    }

    [Theory(
        DisplayName = "AC19 a static view, a static soft-delete class and a static class with an index still "
        + "give one T and nothing else")]
    [InlineData(StaticView)]
    [InlineData(StaticSoft)]
    [InlineData(StaticIndexed)]
    public void Another_static_declaration_gives_one_diagnostic(string source)
    {
        var (diagnostics, generated, output) = Run(source + Sibling);

        Assert.Equal(TypeId, Assert.Single(diagnostics).Id);
        var model = GeneratedModel.Of(output);
        Assert.False(model.Has("SEPurge"));
        Assert.DoesNotContain("SEOrm", generated, StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "AC19 a sibling that names the static type has errors only in the user's source, never in "
        + "the generated file")]
    public void A_sibling_that_names_a_static_type_adds_no_error_of_its_own()
    {
        var (diagnostics, generated, output) = Run(StaticReferrer);

        Assert.Equal(TypeId, Assert.Single(diagnostics).Id);
        var errors = CompileErrors(output);
        Assert.NotEmpty(errors);
        var user = output.SyntaxTrees.First();
        Assert.All(errors, error => Assert.Contains(error.Id, SignatureErrors));
        Assert.All(errors, error => Assert.Same(user, error.Location.SourceTree));
        Assert.DoesNotContain("SEOrm", generated, StringComparison.Ordinal);
        Assert.Contains("class SibOrm", generated, StringComparison.Ordinal);
    }
}
