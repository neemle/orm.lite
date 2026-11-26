using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Neemle.OrmLite.Tests.Corpus;
using Neemle.OrmLite.Tests.OrmLiteGolden;

var target = Path.GetFullPath(args[0]);
var method = typeof(OrmLiteGoldenTests).GetMethod("Generate", BindingFlags.NonPublic | BindingFlags.Static)!;
var values = (IEnumerable)method.Invoke(null, [Directory.GetCurrentDirectory()])!;
Directory.CreateDirectory(target + "/orm");
foreach (var value in values)
{
    var type = value.GetType();
    var name = (string)type.GetField("Item1")!.GetValue(value)!;
    var text = (string)type.GetField("Item2")!.GetValue(value)!;
    File.WriteAllBytes(Path.Combine(target, "orm", name + ".golden"), new UTF8Encoding(false).GetBytes(text));
}
var result = CorpusCatalog.Capture(Directory.GetCurrentDirectory());
CorpusCatalog.Write(target, result);
File.WriteAllText(Path.Combine(target, "capture-facts.json"), JsonSerializer.Serialize(new
{
    CaptureOnly = true, OraclePass = false, Entries = result.Entries.Count, Unaccounted = CorpusIndex.Unaccounted
}));
Console.WriteLine("CAPTURE_ONLY entries=" + result.Entries.Count + "; unaccounted="
    + string.Join(",", CorpusIndex.Unaccounted) + "; not an oracle verification pass");
