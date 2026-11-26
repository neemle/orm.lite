using Neemle.OrmLite.Tests.Corpus;
using Neemle.OrmLite.Tests.OrmLiteGolden;
using System.Reflection;
using System.Text;
var root = Directory.GetCurrentDirectory();
var output = Path.Combine(root, args[0]);
var actual = CorpusCatalog.Capture(root);
CorpusCatalog.Write(Path.Combine(output, "actual"), actual);
var pending = CorpusIndex.Unaccounted.ToArray();
const string reason = "immutable query/policy/On-binding snapshot consumed by the graph planner; "
    + "it emits no statement and is exercised by GraphReadTests, GraphBatchTests and GraphFailureTests";
((IDictionary<string, string>)CorpusSurface.Exclusions).Add("Query.GraphSnapshot/0", reason);
CorpusCatalog.Write(Path.Combine(output, "proposed"), CorpusCatalog.Capture(root));
var generate = typeof(OrmLiteGoldenTests).GetMethod("Generate", BindingFlags.NonPublic | BindingFlags.Static)!;
var sources = (IEnumerable<(string Name, string Text)>)generate.Invoke(null, [root])!;
var golden = Path.Combine(output, "proposed/tests/Tests/OrmLiteGolden/Expected");
Directory.CreateDirectory(golden);
foreach (var (name, text) in sources)
    File.WriteAllText(Path.Combine(golden, name + ".golden"), text, new UTF8Encoding(false));
Console.WriteLine("Unaccounted actual surface: " + string.Join(", ", pending));
Console.WriteLine("PROPOSAL ONLY: current snapshots captured; no governed test/assertion/baseline/exclusion edited.");

var options = (Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider)typeof(OrmLiteGoldenTests).Assembly
    .GetType("Neemle.OrmLite.Tests.GeneratorOptions")!.GetMethod("Pinned", BindingFlags.Static | BindingFlags.Public)!.Invoke(null, null)!;
_ = typeof(Neemle.OrmLite.Query<>);
var refs = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
    .Select(a => a.Location).Concat(((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator))
    .Distinct().Select(x => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(x));
var models = new[] { "[Table(\"lower\")] public class row { [Key] public int Id { get; set; } }",
    "[Table(\"upper\")] public class Row { [Key] public int Id { get; set; } }" };
for (var order = 0; order < 2; order++)
{
    var trees = (order == 0 ? models : models.Reverse()).Select((s,i) => Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
        "using Neemle.OrmLite; namespace Collision; " + s, path: i + ".cs"));
    var comp = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("CaseFixture", trees, refs,
        new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
    var driver = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver.Create(
        new Microsoft.CodeAnalysis.ISourceGenerator[] { Microsoft.CodeAnalysis.GeneratorExtensions.AsSourceGenerator(new Neemle.OrmLite.Generator.OrmGenerator()),
            Microsoft.CodeAnalysis.GeneratorExtensions.AsSourceGenerator(new Neemle.OrmLite.Generator.MigrationGenerator()) }, optionsProvider: options)
        .RunGeneratorsAndUpdateCompilation(comp, out var compiled, out _);
    var result = driver.GetRunResult();
    var folder = Path.Combine(output, "collision", order.ToString()); Directory.CreateDirectory(folder);
    foreach (var item in models.Select((s,i)=>(s,i))) File.WriteAllText(Path.Combine(folder, "input"+item.i+".cs.txt"), item.s);
    foreach (var g in result.Results.SelectMany(r => r.GeneratedSources)) File.WriteAllText(Path.Combine(folder, g.HintName),g.SourceText.ToString());
    File.WriteAllText(Path.Combine(folder,"diagnostics.json"),System.Text.Json.JsonSerializer.Serialize(new {
        generator = result.Diagnostics.Select(d=>d.ToString()), compilation = compiled.GetDiagnostics().Where(d=>d.Severity==Microsoft.CodeAnalysis.DiagnosticSeverity.Error).Select(d=>d.ToString()) },new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine("COLLISION order="+order+" diagnostics="+string.Join(" | ",result.Diagnostics));
}
var helper = typeof(OrmLiteGoldenTests).Assembly.GetType("Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun")!;
var fixture = "[Table(\"docs\")] [SoftDelete] public class Doc { [Key] public Guid Id { get; set; } "
    + "public DateTime? DeletedAt { get; set; } public Guid ScopeId { get; set; } public string Title { get; set; } = \"\"; }\n"
    + "[Migration(\"0001_docs_extra\", Reversibility.Reversible)] public sealed class M0001 : Migration { "
    + "public override void Up(MigrationBuilder b) => b.Raw(\"postgres\", \"SELECT 'up'\"); "
    + "public override void Down(MigrationBuilder b) => b.Raw(\"postgres\", \"SELECT 'down'\"); }";
var runMethod = helper.GetMethod("Run", BindingFlags.Static | BindingFlags.NonPublic)!;
var runOutput = runMethod.Invoke(null, [fixture])!;
var hygieneCompilation = (Microsoft.CodeAnalysis.Compilation)runOutput.GetType().GetField("Item3")!.GetValue(runOutput)!;
var hygiene = Path.Combine(output, "hygiene"); Directory.CreateDirectory(hygiene);
foreach (var tree in hygieneCompilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
    File.WriteAllText(Path.Combine(hygiene, Path.GetFileName(tree.FilePath)), tree.ToString());
