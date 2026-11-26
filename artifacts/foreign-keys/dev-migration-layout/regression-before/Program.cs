using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Neemle.OrmLite.Generator;
using Neemle.OrmLite.Tests.Corpus;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

var path = args[0]; Directory.CreateDirectory(path);
var captured = CorpusCatalog.Capture(Directory.GetCurrentDirectory());
File.WriteAllText(Path.Combine(path, "entries.json"), JsonSerializer.Serialize(captured.Entries));
foreach (var entry in captured.Entries.Where(e => e.Group.StartsWith("generator-")))
{
    var output = Path.Combine(path, entry.Group, entry.Name + ".golden");
    Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.WriteAllText(output, entry.Text);
}
var inputs = new[] {
    "using Neemle.OrmLite; using Neemle.OrmLite.Nodes; [Table(\"case_parent\")] public class row { public int Id {get;set;} }",
    "using Neemle.OrmLite; using Neemle.OrmLite.Nodes; [Table(\"case_child\")] public class Row { public int Id {get;set;} "
        + "public int ParentId {get;set;} [Reference(\"ParentId\")] [ForeignKey(\"fk_case\", ReferentialAction.Restrict)] "
        + "public row? Parent {get;set;} }" };
File.WriteAllText(Path.Combine(path,"case-input.json"),JsonSerializer.Serialize(inputs));
var first = CaptureCase(inputs,Path.Combine(path,"case"));
var second = CaptureCase(inputs.Reverse(),Path.Combine(path,"case-reversed"));
File.WriteAllText(Path.Combine(path,"case-proof.json"),JsonSerializer.Serialize(new {
    first, second, discoveryPermutationEqual=first.Hashes.SequenceEqual(second.Hashes) }));
Console.WriteLine(JsonSerializer.Serialize(new { corpusEntries=captured.Entries.Count,
    generatorEntries=captured.Entries.Count(e=>e.Group.StartsWith("generator-")), first, second }));

static CaseResult CaptureCase(IEnumerable<string> inputs,string path)
{
    Directory.CreateDirectory(path);
    var references=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Select(p=>MetadataReference.CreateFromFile(p));
    var compilation=CSharpCompilation.Create("FkCaseHintProbe",inputs.Select(x=>CSharpSyntaxTree.ParseText(x)),
        references,new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    GeneratorDriver driver=CSharpGeneratorDriver.Create(new OrmGenerator().AsSourceGenerator(),
        new MigrationGenerator().AsSourceGenerator());
    driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out _);
    var result=driver.GetRunResult();
    var sources=result.Results.SelectMany(r=>r.GeneratedSources).OrderBy(s=>s.HintName).ToArray();
    foreach(var source in sources)File.WriteAllText(Path.Combine(path,source.HintName),source.SourceText.ToString());
    return new CaseResult(result.Diagnostics.Concat(output.GetDiagnostics()).Where(d=>d.Severity==DiagnosticSeverity.Error)
        .Select(d=>d.ToString()).ToArray(),sources.Select(s=>s.HintName+"|"+Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(s.SourceText.ToString())))).ToArray());
}

internal sealed record CaseResult(string[] Diagnostics, string[] Hashes);
