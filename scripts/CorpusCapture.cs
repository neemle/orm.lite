#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
#:project ../tests/Neemle.Orm.Lite.Tests/Neemle.Orm.Lite.Tests.csproj
using Neemle.Orm.Lite.Tests.Corpus;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: CorpusCapture.cs capture [--out <directory>] | check");
    return 2;
}

var root = FindRoot();

switch (args[0])
{
    case "capture" when args.Length == 1:
        return Capture(root, root);
    case "capture" when args.Length == 3 && args[1] == "--out":
        return Capture(root, Path.GetFullPath(args[2]));
    case "check" when args.Length == 1:
        return Check(root);
    default:
        Console.Error.WriteLine("unknown command or arguments: " + string.Join(' ', args));
        return 2;
}

string FindRoot()
{
    for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "Neemle.Orm.Lite.slnx"))) return d.FullName;
    throw new InvalidOperationException("Neemle.Orm.Lite.slnx not found above the current directory");
}

int Capture(string sourceRoot, string targetRoot)
{
    var result = CorpusCatalog.Capture(sourceRoot);
    if (CorpusIndex.Unaccounted.Count > 0)
    {
        Console.Error.WriteLine("surface members neither referenced by a case nor excluded with a reason:");
        foreach (var member in CorpusIndex.Unaccounted) Console.Error.WriteLine("  " + member);
        return 3;
    }
    CorpusCatalog.Write(targetRoot, result);
    Console.WriteLine("captured " + result.Entries.Count + " entries into " + Path.Combine(targetRoot, CorpusCatalog.GoldenDirectory));
    return 0;
}

int Check(string sourceRoot)
{
    var result = CorpusCatalog.Capture(sourceRoot);
    if (CorpusIndex.Unaccounted.Count > 0)
    {
        Console.Error.WriteLine("surface members neither referenced by a case nor excluded with a reason:");
        foreach (var member in CorpusIndex.Unaccounted) Console.Error.WriteLine("  " + member);
        return 3;
    }
    var problems = CorpusCatalog.Compare(sourceRoot, result);
    foreach (var problem in problems) Console.Error.WriteLine(problem);
    Console.WriteLine(problems.Count == 0 ? "corpus matches: " + result.Entries.Count + " entries" : problems.Count + " difference(s)");
    return problems.Count == 0 ? 0 : 1;
}
