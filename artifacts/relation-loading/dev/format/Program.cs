using System.Reflection;
using Neemle.OrmLite.Generator;

var formatter = typeof(OrmGenerator).Assembly.GetType("Neemle.OrmLite.Generator.GraphEmitter")!
    .GetMethod("WrapMembers", BindingFlags.Static | BindingFlags.NonPublic)!;
foreach (var file in File.ReadAllLines(args[0]))
{
    if (!file.StartsWith("src/", StringComparison.Ordinal)) throw new InvalidOperationException("source list only");
    var text = File.ReadAllText(file);
    var wrapped = (string)formatter.Invoke(null, [text])!;
    if (wrapped != text) File.WriteAllText(file, wrapped);
}
