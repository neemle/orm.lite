using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Neemle.OrmLite.Generator;

var root = Directory.GetCurrentDirectory();
var tests = Path.GetFullPath(args[0]);
var oldBuild = Path.Combine(root, "artifacts/foreign-keys/demo-parity/jit10-build/obj/ForeignKeysDemo/release");
var config = Path.Combine(oldBuild, "ForeignKeysDemo.GeneratedMSBuildEditorConfig.editorconfig");
var options = File.ReadAllLines(config).Where(line => line.StartsWith("build_property.", StringComparison.Ordinal))
    .Select(line => line.Split('=', 2)).ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim());
var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
    .Concat(Directory.GetFiles(tests, "*.dll")).GroupBy(Path.GetFileName).Select(group => group.Last());
var references = paths.Select(path => MetadataReference.CreateFromFile(path));
var source = Directory.GetFiles(Path.Combine(root, "examples/foreign-keys"), "*.cs")
    .Append(Path.Combine(oldBuild, "ForeignKeysDemo.GlobalUsings.g.cs"));
var trees = source.Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path));
var compilation = CSharpCompilation.Create("ForeignKeysDemo", trees, references,
    new CSharpCompilationOptions(OutputKind.ConsoleApplication, nullableContextOptions: NullableContextOptions.Enable));
var cache = Path.Combine(root, "artifacts/foreign-keys/demo-parity/nuget");
var capabilities = new[] { "postgres", "sqlite" }.Select(provider => (AdditionalText)new FileText(Path.Combine(cache,
    "neemle.ormlite." + provider, "0.1.0-dev.g39a20f2", "buildTransitive", "ormlite.capabilities." + provider + ".txt")));
GeneratorDriver driver = CSharpGeneratorDriver.Create(
    [new OrmGenerator().AsSourceGenerator(), new MigrationGenerator().AsSourceGenerator()],
    capabilities.ToImmutableArray(), null, new OptionsProvider(options));
driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
var errors = diagnostics.Concat(output.GetDiagnostics()).Where(issue => issue.Severity == DiagnosticSeverity.Error).ToArray();
if (errors.Length != 0) throw new InvalidOperationException(string.Join("\n", errors.Select(error => error.ToString())));
Directory.CreateDirectory(args[1]);
var results = new SortedDictionary<string, string[]>(StringComparer.Ordinal);
foreach (var generated in driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources))
{
    File.WriteAllText(Path.Combine(args[1], generated.HintName), generated.SourceText.ToString());
    var unit = CSharpSyntaxTree.ParseText(generated.SourceText).GetCompilationUnitRoot();
    foreach (var type in unit.DescendantNodes().OfType<TypeDeclarationSyntax>().Where(type => type.Parent is BaseNamespaceDeclarationSyntax))
        results.Add(type.Identifier.ValueText, type.DescendantTokens().Select(token => token.RawKind + ":" + token.Text).ToArray());
}
File.WriteAllText(Path.Combine(args[1], "tokens.json"), JsonSerializer.Serialize(results));
Console.WriteLine("SEMANTIC_CAPTURE types=" + results.Count + " compileErrors=0");

sealed class FileText(string path) : AdditionalText
{
    public override string Path => path;
    public override Microsoft.CodeAnalysis.Text.SourceText GetText(CancellationToken cancellationToken = default) =>
        Microsoft.CodeAnalysis.Text.SourceText.From(File.ReadAllText(path));
}
sealed class OptionsProvider(Dictionary<string, string> values) : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions { get; } = new Values(values);
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    sealed class Values(Dictionary<string, string> data) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => data.TryGetValue(key, out value);
    }
}
