#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using Shipped = (string Entry, string Assembly, string File, string Info);

// PackVersions guards the version stamped into every assembly the three packages ship (ORML-39). The version is set
// once, in Directory.Build.props (OrmLiteVersion); a project that does not take it keeps the SDK default 1.0.0, and a
// provider/runtime mismatch could not be told from the assembly identity of a bug report.
//   check <folder> <version>   every .dll inside every Neemle.OrmLite*.nupkg of the folder has the package version as
//                              AssemblyVersion (numeric part, four parts), FileVersion (the same) and
//                              InformationalVersion (exactly the package version); at least five assemblies are found
//   self-test                  the decision refuses what it is meant to refuse and accepts what it must accept
// scripts/PackCheck.cs runs `check` as a child process, like the README guard.
internal static class PackVersions
{
    // The core package ships the generator, the runtime and the Sql kit; each provider package ships its own.
    private const int ExpectedAssemblies = 5;

    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "self-test") return SelfTest();
        if (args.Length == 3 && args[0] == "check") return Check(args[1], args[2]);
        Console.Error.WriteLine("usage: PackVersions.cs check <folder of nupkg files> <package version> | self-test");
        return 2;
    }

    // The numeric part of a package version, padded to the four parts of an assembly version: 0.1.0-pre is 0.1.0.0.
    private static string Numeric(string version)
    {
        var parts = version.Split('-', '+')[0].Split('.').ToList();
        while (parts.Count < 4) parts.Add("0");
        return string.Join('.', parts);
    }

    private static IEnumerable<string> Problems(Shipped shipped, string version)
    {
        var numeric = Numeric(version);
        if (shipped.Assembly != numeric)
            yield return shipped.Entry + ": AssemblyVersion is " + shipped.Assembly + ", expected " + numeric;
        if (shipped.File != numeric)
            yield return shipped.Entry + ": FileVersion is " + shipped.File + ", expected " + numeric;
        if (shipped.Info != version)
            yield return shipped.Entry + ": InformationalVersion is " + shipped.Info + ", expected " + version;
    }

    // Every .dll of one package, extracted into the work folder and read without loading it.
    private static IEnumerable<Shipped> Read(string package, string work)
    {
        using var zip = ZipFile.OpenRead(package);
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            var file = Path.Combine(work, Guid.NewGuid().ToString("N") + ".dll");
            entry.ExtractToFile(file);
            var info = FileVersionInfo.GetVersionInfo(file);
            var name = Path.GetFileName(package) + "!" + entry.FullName;
            yield return (name, AssemblyName.GetAssemblyName(file).Version?.ToString() ?? "",
                info.FileVersion ?? "", info.ProductVersion ?? "");
        }
    }

    private static int Check(string folder, string version)
    {
        var work = Path.Combine(Path.GetTempPath(), "orm-lite-versions-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var packages = Directory.GetFiles(folder, "Neemle.OrmLite*.nupkg");
            var shipped = packages.SelectMany(package => Read(package, work).ToList()).ToList();
            var problems = shipped.SelectMany(s => Problems(s, version)).ToList();
            if (shipped.Count < ExpectedAssemblies)
                problems.Add("found " + shipped.Count + " assemblies in " + packages.Length + " packages of " + folder
                    + ", expected at least " + ExpectedAssemblies);
            foreach (var problem in problems) Console.Error.WriteLine("FAIL: " + problem);
            if (problems.Count == 0)
                Console.WriteLine("PASS: " + shipped.Count + " shipped assemblies carry the version " + version + ".");
            return problems.Count == 0 ? 0 : 1;
        }
        finally
        {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }

    private static int SelfTest()
    {
        var failed = 0;

        void Case(string name, int expected, Shipped shipped, string version)
        {
            var got = Problems(shipped, version).Count();
            var verdict = got == expected ? "ok   " : "FAIL ";
            Console.WriteLine(verdict + name + ": " + got + " problems, expected " + expected);
            if (got != expected) failed++;
        }

        Case("stamped", 0, ("e", "0.1.0.0", "0.1.0.0", "0.1.0-pre"), "0.1.0-pre");
        Case("sdk default", 3, ("e", "1.0.0.0", "1.0.0.0", "1.0.0"), "0.1.0-pre");
        Case("assembly version only", 1, ("e", "1.0.0.0", "0.1.0.0", "0.1.0-pre"), "0.1.0-pre");
        Case("file version only", 1, ("e", "0.1.0.0", "1.0.0.0", "0.1.0-pre"), "0.1.0-pre");
        Case("revision suffix", 1, ("e", "0.1.0.0", "0.1.0.0", "0.1.0-pre+3f2a"), "0.1.0-pre");
        Case("release", 0, ("e", "2.4.1.0", "2.4.1.0", "2.4.1"), "2.4.1");
        Case("prerelease label", 0, ("e", "2.0.0.0", "2.0.0.0", "2.0.0-rc.1"), "2.0.0-rc.1");
        Console.WriteLine(failed == 0 ? "PASS: the version guard holds its own cases."
            : "FAIL: the version guard does not hold its own cases.");
        return failed == 0 ? 0 : 1;
    }
}
