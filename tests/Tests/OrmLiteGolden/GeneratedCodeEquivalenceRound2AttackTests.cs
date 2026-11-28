using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Neemle.OrmLite.Tests.OrmLiteGolden;

// ORML-156 / §803 FULL / round 2: QA finding R2-1. A verbatim attribute name turns off the `Attribute`-suffix
// lookup, so `[@Audit]` binds differently from `[Audit]` and the oracle must call them different code.
public sealed class GeneratedCodeEquivalenceRound2AttackTests
{
    private static readonly CSharpParseOptions ParseOptions =
        new(LanguageVersion.Latest);

    private static SyntaxTree Parsed(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, ParseOptions);
        Assert.DoesNotContain(
            tree.GetDiagnostics(),
            d => d.Severity == DiagnosticSeverity.Error);
        return tree;
    }

    private static MetadataReference[] PlatformReferences()
    {
        var paths = Assert.IsType<string>(
            AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"));

        return paths.Split(Path.PathSeparator)
            .Select(path =>
                (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    [Theory]
    [InlineData(
        "public sealed class AuditAttribute : System.Attribute {}\n" +
        "[Audit] public class C {}\n",
        "[Audit]",
        "[@Audit]")]
    [InlineData(
        "internal static class OrmMetaInit {\n" +
        "[global::System.Runtime.CompilerServices.ModuleInitializer]\n" +
        "internal static void Init() {}\n" +
        "}\n",
        "Services.ModuleInitializer]",
        "Services.@ModuleInitializer]")]
    public void R2_1_Verbatim_attribute_names_change_binding(
        string expected, string from, string to)
    {
        Assert.Contains(from, expected);
        var actual = expected.Replace(from, to, StringComparison.Ordinal);
        Assert.NotEqual(expected, actual);

        var expectedTree = Parsed(expected);
        var actualTree = Parsed(actual);
        var references = PlatformReferences();
        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary);

        var expectedCompilation = CSharpCompilation.Create(
            "R2Expected",
            new[] { expectedTree },
            references,
            options);

        var actualCompilation = CSharpCompilation.Create(
            "R2Actual",
            new[] { actualTree },
            references,
            options);

        // Establish the binding difference before testing the oracle.
        Assert.DoesNotContain(
            expectedCompilation.GetDiagnostics(),
            d => d.Severity == DiagnosticSeverity.Error);

        Assert.Contains(
            actualCompilation.GetDiagnostics(),
            d => d.Severity == DiagnosticSeverity.Error);

        // Fails on 3e5ae3a: the oracle returns null.
        Assert.NotNull(
            GeneratedCodeEquivalence.Difference(expected, actual));
    }
}
