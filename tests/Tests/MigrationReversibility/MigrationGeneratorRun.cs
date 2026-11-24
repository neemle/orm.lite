using System.Collections.Immutable;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Neemle.OrmLite.Generator;
using Neemle.OrmLite;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Neemle.OrmLite.Tests.MigrationReversibility;

internal static class MigrationGeneratorRun
{
    private const string Header = "using System; using Neemle.OrmLite; namespace Fixture;\n";

    private static int assemblyCounter;

    internal static string MigrationClass(string name, string version, string reversibility, string members = "", string baseType = "Migration") =>
        $"[Migration(\"{version}\", Reversibility.{reversibility})] public sealed class {name} : {baseType} "
        + "{ public override void Up(MigrationBuilder b) => b.Raw(\"postgres\", \"SELECT 'up'\"); " + members + " }\n";

    internal static string DownOverride(string sql) =>
        "public override void Down(MigrationBuilder b) => b.Raw(\"postgres\", \"" + sql + "\");";

    // A file cannot hold a file-scoped namespace and a block namespace (CS8955). A source that declares block
    // namespaces of its own is therefore wrapped in a block namespace Fixture instead of getting the file-scoped
    // header; the types still live under Fixture, so the full names the tests assert are the same.
    private const string BlockHeader = "using System; using Neemle.OrmLite;\nnamespace Fixture\n{\n";

    private static readonly Regex BlockNamespace = new(@"(?m)^\s*namespace\s+[\w.]+\s*(\{|$)");

    internal static (ImmutableArray<Diagnostic> Diagnostics, string Generated, Compilation Output)
        Run(string source) =>
        RunWhole(BlockNamespace.IsMatch(source) ? BlockHeader + source + "\n}\n" : Header + source);

    internal static (ImmutableArray<Diagnostic> Diagnostics, string Generated, Compilation Output) RunWhole(string source)
    {
        var compilation = CSharpCompilation.Create(
            "MigrationFixture" + Interlocked.Increment(ref assemblyCounter),
            new[] { CSharpSyntaxTree.ParseText(source) },
            GeneratorReferences.All,
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
        var type = assembly.GetType("Neemle.OrmLite.Generated." + output.AssemblyName + "Migrations", throwOnError: true)!;
        return (MigrationContribution[])type.GetField("Contributions")!.GetValue(null)!;
    }
}
