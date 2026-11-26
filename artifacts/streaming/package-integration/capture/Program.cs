using System.Collections;
using System.Reflection;
using System.Text;
using Neemle.OrmLite.Tests.Corpus;
using Neemle.OrmLite.Tests.OrmLiteGolden;

var target = Path.GetFullPath(args[0]);
var method = typeof(OrmLiteGoldenTests).GetMethod("Generate", BindingFlags.NonPublic | BindingFlags.Static)!;
var values = (IEnumerable)method.Invoke(null, [Directory.GetCurrentDirectory()])!;
Directory.CreateDirectory(Path.Combine(target, "orm"));
foreach (var value in values)
{
    var type = value.GetType();
    var name = (string)type.GetField("Item1")!.GetValue(value)!;
    var text = (string)type.GetField("Item2")!.GetValue(value)!;
    File.WriteAllBytes(Path.Combine(target, "orm", name + ".golden"), new UTF8Encoding(false).GetBytes(text));
}
var corpus = CorpusCatalog.Capture(Directory.GetCurrentDirectory());
if (CorpusIndex.Unaccounted.Count != 0)
    throw new InvalidOperationException("unaccounted corpus members: " + string.Join(",", CorpusIndex.Unaccounted));
CorpusCatalog.Write(target, corpus);
Console.WriteLine("CAPTURE_ONLY entries=" + corpus.Entries.Count + "; not an oracle verification pass");
