using System.Collections.Immutable;
using System.Runtime.Loader;
using Neemle.Orm.Lite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Neemle.Orm.Lite.Tests.MigrationReversibility;

internal static class MigrationGeneratorRun
{
    private const string Header = "using System; using Neemle.Orm.Lite; namespace Fixture;\n";

    private static int assemblyCounter;

    internal static string MigrationClass(string name, string version, string reversibility, string members = "", string baseType = "Migration") =>
        $"[Migration(\"{version}\", Reversibility.{reversibility})] public sealed class {name} : {baseType} "
        + "{ public override void Up(MigrationBuilder b) => b.Sql(\"SELECT 'up'\"); " + members + " }\n";

    internal static string DownOverride(string sql) =>
        "public override void Down(MigrationBuilder b) => b.Sql(\"" + sql + "\");";

    internal static (ImmutableArray<Diagnostic> Diagnostics, string Generated, Compilation Output) Run(string source)
    {
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
            .Select(a => (MetadataReference)MetadataReference.CreateFromFile(a.Location))
            .ToList();

        var compilation = CSharpCompilation.Create(
            "MigrationFixture" + Interlocked.Increment(ref assemblyCounter),
            new[] { CSharpSyntaxTree.ParseText(Header + source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver.Create([new OrmGenerator().AsSourceGenerator(), new MigrationGenerator().AsSourceGenerator()], null, null, GeneratorOptions.Pinned())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var run = driver.GetRunResult();

        var generated = string.Join("\n", run.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.SourceText.ToString()));
        return (run.Diagnostics, generated, output);
    }

    internal static IReadOnlyList<MigrationContribution> GeneratedContributions(Compilation output)
    {
        using var stream = new MemoryStream();
        var emitted = output.Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        stream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        var type = assembly.GetType("Neemle.Orm.Lite.Generated." + output.AssemblyName + "Migrations", throwOnError: true)!;
        return (MigrationContribution[])type.GetField("Contributions")!.GetValue(null)!;
    }
}
