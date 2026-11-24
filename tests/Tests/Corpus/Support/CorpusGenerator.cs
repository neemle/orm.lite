using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Neemle.OrmLite.Generator;

namespace Neemle.OrmLite.Tests.Corpus;

internal sealed record GeneratorSet(
    string Name,
    string AssemblyName,
    Func<string, IReadOnlyList<(string Path, string Text)>> Sources,
    GeneratorOptions Options,
    bool Execute);

internal sealed record GeneratorResult(
    IReadOnlyList<(string Hint, string Text)> Sources,
    IReadOnlyList<string> Diagnostics,
    Compilation Output);

internal static class CorpusGenerator
{
    private const string ImplicitUsings =
        "global using System; global using System.Collections.Generic; global using System.IO; "
        + "global using System.Linq; global using System.Net.Http; global using System.Threading; "
        + "global using System.Threading.Tasks;";

    private static readonly MetadataReference[] References =
    [
        .. (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
            .Split(Path.PathSeparator)
            .Where(p => p.Length > 0 && Path.GetFileNameWithoutExtension(p) != "Neemle.OrmLite.Tests")
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
    ];

    public static GeneratorResult Run(GeneratorSet set, string root)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = set.Sources(root)
            .OrderBy(s => s.Path, StringComparer.Ordinal)
            .Select(s => CSharpSyntaxTree.ParseText(s.Text, parse, s.Path))
            .Append(CSharpSyntaxTree.ParseText(ImplicitUsings, parse, "ImplicitUsings.cs"))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            set.AssemblyName,
            trees,
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var driver = CSharpGeneratorDriver
            .Create([new OrmGenerator().AsSourceGenerator(), new MigrationGenerator().AsSourceGenerator()], parseOptions: parse, optionsProvider: set.Options)
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var run = driver.GetRunResult();
        var sources = run.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => (s.HintName, s.SourceText.ToString()))
            .OrderBy(s => s.HintName, StringComparer.Ordinal)
            .ToList();
        var diagnostics = run.Diagnostics
            .Select(Describe)
            .OrderBy(d => d, StringComparer.Ordinal)
            .ToList();
        return new GeneratorResult(sources, diagnostics, output);
    }

    private static string Describe(Diagnostic d)
    {
        var span = d.Location.GetLineSpan();
        var where = d.Location == Location.None ? "no location" : Path.GetFileName(span.Path) + ":" + (span.StartLinePosition.Line + 1);
        return d.Id + " " + d.Severity + " " + where + ": " + d.GetMessage();
    }

    public static string CompileDiagnostics(Compilation output)
    {
        using var stream = new MemoryStream();
        var emitted = output.Emit(stream);
        return emitted.Success ? "" : string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()));
    }

    public static IsolatedProbe Load(Compilation output)
    {
        var stream = new MemoryStream();
        var emitted = output.Emit(stream);
        if (!emitted.Success)
            throw new InvalidOperationException("the generated fixture did not compile: " + string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        stream.Position = 0;
        var context = new IsolatedContext(typeof(OrmContext).Assembly.Location);
        var assembly = context.LoadFromStream(stream);
        return new IsolatedProbe(context, assembly);
    }

    internal sealed class IsolatedContext(string runtimePath) : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) =>
            assemblyName.Name switch
            {
                "Neemle.OrmLite.Runtime" => LoadFromAssemblyPath(runtimePath),
                "Neemle.OrmLite.Sql" => LoadFromAssemblyPath(Path.Combine(Path.GetDirectoryName(runtimePath)!, "Neemle.OrmLite.Sql.dll")),
                "Neemle.OrmLite.Postgres" => LoadFromAssemblyPath(Path.Combine(Path.GetDirectoryName(runtimePath)!, "Neemle.OrmLite.Postgres.dll")),
                _ => null,
            };
    }

    internal sealed class IsolatedProbe(IsolatedContext context, Assembly assembly) : IDisposable
    {
        private readonly Type _probe = assembly.GetType("CorpusProbe.Probe", throwOnError: true)!;

        public string Dump(string token) => (string)_probe.GetMethod("Dump")!.Invoke(null, [token])!;

        public string[] Versions(string token) => (string[])_probe.GetMethod("Versions")!.Invoke(null, [token])!;

        public Task RunMigrations(System.Data.Common.DbDataSource source, string token) =>
            (Task)_probe.GetMethod("RunMigrations")!.Invoke(null, [source, token])!;

        public void Dispose() => context.Unload();
    }
}
