using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Neemle.Orm.Lite.Tests.IdentifierLimits;

// Runs the generator over one source text with the provider and capability manifest a project would pass.
internal static class ManifestHarness
{
    private const string Header =
        "using System; using System.Collections.Generic; using Neemle.Orm.Lite; namespace Fixture;\n";

    private sealed class ManifestText(string provider, string text) : AdditionalText
    {
        public override string Path { get; } = "/assets/ormlite.capabilities." + provider + ".txt";

        public override SourceText GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(text, Encoding.UTF8);
    }

    private sealed class Options(string providers) : AnalyzerConfigOptionsProvider
    {
        private sealed class Bag(string providers) : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
            {
                value = key == "build_property.OrmLiteProviders" ? providers : null;
                return value is not null;
            }
        }

        public override AnalyzerConfigOptions GlobalOptions { get; } = new Bag(providers);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }

    internal static string Manifest(string? limit)
    {
        var levels = Enum.GetValues<Capability>().Select(c => c + "=Native");
        return string.Join("\n", levels) + "\n" + (limit is null ? "" : "MaxIdentifierBytes=" + limit + "\n");
    }

    internal static IReadOnlyList<Diagnostic> Run(string source, string provider, string manifest)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();
        var options = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable);
        var compilation = CSharpCompilation.Create(
            "LimitFixture", [CSharpSyntaxTree.ParseText(Header + source)], references, options);
        var driver = CSharpGeneratorDriver.Create(
            [new OrmGenerator().AsSourceGenerator()],
            [new ManifestText(provider, manifest)],
            null,
            new Options(provider));
        return driver.RunGenerators(compilation).GetRunResult().Diagnostics
            .Where(d => d.Id == "ORMLITE034").ToList();
    }
}
