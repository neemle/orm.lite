using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Neemle.OrmLite;
using Neemle.OrmLite.Generator;
using System.Text.Json;

Console.WriteLine(typeof(GraphModel).Assembly.FullName);
var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
    .Append(typeof(GraphModel).Assembly.Location).Distinct().ToArray();
var references = paths.Select(path => MetadataReference.CreateFromFile(path)).ToArray();
var report = new List<object>();
foreach (var input in ProbeInputs.All())
{
    var folder = Path.Combine(args[0], input.Name);
    Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder, "input.cs.txt"), input.Source);
    var compilation = CSharpCompilation.Create("PolicyProbe", [CSharpSyntaxTree.ParseText(input.Source, path: "input.cs")],
        references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    var driver = CSharpGeneratorDriver.Create(new OrmGenerator(), new MigrationGenerator())
        .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
    var result = driver.GetRunResult();
    var sources = result.Results.SelectMany(r => r.GeneratedSources).ToArray();
    foreach (var source in sources) File.WriteAllText(Path.Combine(folder, source.HintName), source.SourceText.ToString());
    var diagnostics = result.Diagnostics.Select(d => new { d.Id, severity = d.Severity.ToString(), message = d.GetMessage() }).ToArray();
    var errors = output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray();
    var hasPolicyError = result.Diagnostics.Any(d => d.Id == "ORMLITE035");
    var registered = sources.Any(s => s.SourceText.ToString().Contains("RelationPolicies.Register", StringComparison.Ordinal));
    var pass = input.Invalid ? hasPolicyError && !registered : result.Diagnostics.Length == 0 && errors.Length == 0;
    report.Add(new { input.Name, input.Invalid, pass, registered, diagnostics, compilerErrors = errors });
    File.WriteAllText(Path.Combine(folder, "diagnostics.json"), JsonSerializer.Serialize(report[^1], new JsonSerializerOptions { WriteIndented = true }));
    if (!pass) throw new InvalidOperationException("Probe failed: " + input.Name + " " + string.Join(" | ", errors));
}
File.WriteAllText(Path.Combine(args[0], "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("generator cases=" + report.Count + " PASS");
