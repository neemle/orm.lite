using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Neemle.OrmLite.Tests.Gates;

// Admitted reviewer tests, kept as regression guards (provenance: ORML-147, §803 FULL, round 1). Committed with their
// assertions unchanged. F2_Reflective_escape_must_be_reported (reflection by string name) is left out: that gap is
// ORML-148's, pending a decision there.
public sealed class ComposerEscapeFullReviewTests
{
    private static readonly Assembly Composer =
        Assembly.Load(new AssemblyName("Neemle.CSharpComposer"));

    private static readonly MetadataReference[] References = BuildReferences();

    private static string ProbePath => Path.Combine(
        RuntimeSources.LibraryRoot(), "src", "Generator", "ComposerEscapeProbe.cs");

    [Fact]
    public void F1_Build_active_conditional_escape_must_be_reported()
    {
        const string source = """
            public static class Probe
            {
                public static object Run()
                {
            #if NET10_0
                    return global::Neemle.CSharpComposer.Cs.RawExpression("x");
            #else
                    return null!;
            #endif
                }
            }
            """;

        var compilation = Compile(source, "NET10_0");
        var tree = compilation.SyntaxTrees.Single();
        var invocation = tree.GetRoot().DescendantNodes()
            .OfType<InvocationExpressionSyntax>().Single();

        var method = Assert.IsAssignableFrom<IMethodSymbol>(
            compilation.GetSemanticModel(tree).GetSymbolInfo(invocation).Symbol);

        Assert.Equal("RawExpression", method.Name);
        Assert.Equal("Neemle.CSharpComposer", method.ContainingAssembly.Name);

        var result = Execute(compilation);
        Assert.NotNull(result);
        Assert.Same(Composer, result!.GetType().Assembly);

        // Expected failure: the scanner parses without NET10_0.
        Assert.NotEmpty(Scan(source));
    }

    [Theory]
    [InlineData("""
        class Helper
        {
            static void Reformat() { }
            static void Run() => Reformat();
        }
        """)]
    [InlineData("""
        class Cs
        {
            public static object Parse(string text) => text;
        }
        class Helper
        {
            static object Run() => Cs.Parse("x");
        }
        """)]
    public void F3_Resolved_non_composer_helpers_must_be_allowed(string source)
    {
        var compilation = Compile(source);
        var tree = compilation.SyntaxTrees.Single();
        var invocation = tree.GetRoot().DescendantNodes()
            .OfType<InvocationExpressionSyntax>().Single();

        var method = Assert.IsAssignableFrom<IMethodSymbol>(
            compilation.GetSemanticModel(tree).GetSymbolInfo(invocation).Symbol);

        Assert.NotEqual(
            "Neemle.CSharpComposer", method.ContainingAssembly.Name);

        // Expected failure: successful non-Composer binding falls through.
        Assert.Empty(Scan(source));
    }

    [Fact]
    public void F4_Nameof_must_not_count_as_an_escape()
    {
        const string source = """
            public static class Probe
            {
                public const string Name =
                    nameof(global::Neemle.CSharpComposer.Cs.Parse);
            }
            """;

        var compilation = Compile(source);
        var probe = compilation.GetTypeByMetadataName("Probe");
        Assert.NotNull(probe);

        var field = Assert.IsAssignableFrom<IFieldSymbol>(
            probe!.GetMembers("Name").Single());

        Assert.True(field.HasConstantValue);
        Assert.Equal("Parse", Assert.IsType<string>(field.ConstantValue));

        // Expected failure: the method symbol/candidates are reported.
        Assert.Empty(Scan(source));
    }

    private static IReadOnlyList<string> Scan(string source) =>
        ComposerEscapeScan.Find(new[] { (Path: ProbePath, Text: source) });

    private static CSharpCompilation Compile(
        string source, params string[] symbols)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest)
            .WithPreprocessorSymbols(symbols);

        var tree = CSharpSyntaxTree.ParseText(source, options, ProbePath);
        var compilation = CSharpCompilation.Create(
            "ComposerEscapeProbe_" + Guid.NewGuid().ToString("N"),
            new[] { tree },
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        Assert.Empty(compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error));

        return compilation;
    }

    private static object? Execute(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);

        Assert.True(
            emitted.Success,
            string.Join(Environment.NewLine, emitted.Diagnostics));

        var assembly = Assembly.Load(stream.ToArray());
        var probe = assembly.GetType("Probe", throwOnError: true)!;
        var run = probe.GetMethod(
            "Run", BindingFlags.Public | BindingFlags.Static)!;

        return run.Invoke(null, null);
    }

    private static MetadataReference[] BuildReferences()
    {
        var references = GeneratorReferences.All.ToList();
        var present = references.OfType<PortableExecutableReference>()
            .Any(r => string.Equals(
                r.FilePath, Composer.Location, StringComparison.Ordinal));

        if (!present)
            references.Add(MetadataReference.CreateFromFile(Composer.Location));

        return references.ToArray();
    }
}
