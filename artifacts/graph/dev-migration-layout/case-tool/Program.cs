using Neemle.OrmLite.Tests.OrmLiteGolden;
using Microsoft.CodeAnalysis;
using System.Reflection;
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
Console.WriteLine(typeof(Neemle.OrmLite.Query<>).Assembly.FullName);
var options = (Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider)typeof(OrmLiteGoldenTests).Assembly
    .GetType("Neemle.OrmLite.Tests.GeneratorOptions")!.GetMethod("Pinned", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, null)!;
_ = typeof(Neemle.OrmLite.Query<>);
var refs = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
    .Select(a => a.Location).Concat(((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator))
    .Distinct().Select(x => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(x));
var models = new[] { "[Table(\"lower\")] public class row { [Key] public int Id { get; set; } }",
    "[Table(\"upper\")] public class Row { [Key] public int Id { get; set; } }" };
var generatorAssembly = args.Length > 1 ? Assembly.LoadFile(Path.GetFullPath(args[1])) : typeof(Neemle.OrmLite.Generator.OrmGenerator).Assembly;
var fixedModels = models.Select((text,index)=>(text,path:index+".cs")).ToArray();
for (var order = 0; order < 2; order++)
{
    var trees = (order == 0 ? fixedModels : fixedModels.Reverse()).Select(pair => Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
        "using Neemle.OrmLite; namespace Collision; " + pair.text, path: pair.path));
    var comp = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("CaseFixture", trees, refs,
        new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
    var driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create(
        new Microsoft.CodeAnalysis.ISourceGenerator[] { Microsoft.CodeAnalysis.GeneratorExtensions.AsSourceGenerator((IIncrementalGenerator)Activator.CreateInstance(generatorAssembly.GetType("Neemle.OrmLite.Generator.OrmGenerator")!)!),
            Microsoft.CodeAnalysis.GeneratorExtensions.AsSourceGenerator((IIncrementalGenerator)Activator.CreateInstance(generatorAssembly.GetType("Neemle.OrmLite.Generator.MigrationGenerator")!)!) }, optionsProvider: options)
        .RunGeneratorsAndUpdateCompilation(comp, out var compiled, out _);
    var result = driver.GetRunResult();
    var folder = Path.Combine(output, "collision", order.ToString()); Directory.CreateDirectory(folder);
    foreach (var item in models.Select((s,i)=>(s,i))) File.WriteAllText(Path.Combine(folder, "input"+item.i+".cs.txt"), item.s);
    foreach (var g in result.Results.SelectMany(r => r.GeneratedSources)) File.WriteAllText(Path.Combine(folder, g.HintName),g.SourceText.ToString());
    File.WriteAllText(Path.Combine(folder,"diagnostics.json"),System.Text.Json.JsonSerializer.Serialize(new {
        generator = result.Diagnostics.Select(d=>d.ToString()), compilation = compiled.GetDiagnostics().Where(d=>d.Severity==Microsoft.CodeAnalysis.DiagnosticSeverity.Error).Select(d=>d.ToString()) },new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine("COLLISION order="+order+" diagnostics="+string.Join(" | ",result.Diagnostics));
}
