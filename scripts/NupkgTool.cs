#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: NupkgTool.cs entries <nupkg> | show <nupkg> <entry> | nuspec <nupkg>");
    return 2;
}

static SortedDictionary<string, string> Entries(string nupkg)
{
    var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
    using var zip = ZipFile.OpenRead(nupkg);
    foreach (var e in zip.Entries)
    {
        if (e.FullName.EndsWith('/') || e.FullName is "[Content_Types].xml" or "_rels/.rels" || e.FullName.StartsWith("package/services/", StringComparison.Ordinal))
            continue;
        if (e.FullName.EndsWith(".nuspec", StringComparison.Ordinal)) continue;
        using var s = e.Open();
        map[e.FullName] = Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
    }
    return map;
}

switch (args[0])
{
    case "entries":
        foreach (var (name, hash) in Entries(args[1])) Console.WriteLine(hash + "\t" + name);
        return 0;
    case "show":
    {
        using var zip = ZipFile.OpenRead(args[1]);
        var entry = zip.GetEntry(args[2]);
        if (entry is null) { Console.Error.WriteLine("no such entry: " + args[2]); return 1; }
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        Console.Write(reader.ReadToEnd());
        return 0;
    }
    case "nuspec":
    {
        using var zip = ZipFile.OpenRead(args[1]);
        var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        if (entry is null) { Console.Error.WriteLine("no nuspec"); return 1; }
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        Console.Write(reader.ReadToEnd());
        return 0;
    }
    default:
        Console.Error.WriteLine("unknown command " + args[0]);
        return 2;
}
