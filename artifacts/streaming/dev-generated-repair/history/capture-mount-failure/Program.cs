using Neemle.OrmLite.Tests.Corpus;
using System.Text.Json;
using System.Text.RegularExpressions;
var proposal = File.ReadAllText("artifacts/streaming/dev-lock-repair/control-proposal/tests/Tests/Corpus/Support/CorpusSurface.cs");
var exclusions = (IDictionary<string,string>)CorpusSurface.Exclusions;
foreach (Match m in Regex.Matches(proposal, "\\[\"(Query\\.(?:Stream|get_Stream)[^\"]+)\"\\] = \"([^\"]+)\""))
    exclusions[m.Groups[1].Value] = m.Groups[2].Value;
var captured = CorpusCatalog.Capture(Directory.GetCurrentDirectory());
Directory.CreateDirectory(args[0]);
File.WriteAllText(Path.Combine(args[0],"entries.json"), JsonSerializer.Serialize(captured.Entries));
File.WriteAllText(Path.Combine(args[0],"INDEX.md"),captured.Index);
foreach(var e in captured.Entries.Where(e=>e.Group.StartsWith("generator-")))
{
    var path=Path.Combine(args[0],e.Group,e.Name+".golden");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,e.Text);
}
Console.WriteLine($"Captured {captured.Entries.Count} entries, {captured.Entries.Count(e=>e.Group.StartsWith("generator-"))} generator entries");
