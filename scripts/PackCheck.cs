#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
using System.Diagnostics;
using System.IO.Compression;
using System.Text;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: PackCheck.cs package <nupkg> | demo-guard");
    return 2;
}

var root = FindRoot();

switch (args[0])
{
    case "package" when args.Length == 2:
        return PackageChecks(ResolvePackage(Path.GetFullPath(args[1])));
    case "demo-guard":
        return DemoGuard();
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

string ResolvePackage(string path)
{
    if (!Directory.Exists(path)) return path;
    var found = Directory.GetFiles(path, "Neemle.Orm.Lite.*.nupkg");
    return found.Length == 1 ? found[0] : path;
}

int DemoGuard()
{
    var csproj = File.ReadAllText(Path.Combine(root, "demo", "OrmLiteDemo.csproj"));
    var bad = false;
    if (csproj.Contains("ProjectReference", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("FAIL: the demo must reference the package, not a project");
        bad = true;
    }
    if (!csproj.Contains("<PackageReference Include=\"Neemle.Orm.Lite\"", StringComparison.Ordinal))
    {
        Console.Error.WriteLine("FAIL: the demo does not reference the Neemle.Orm.Lite package");
        bad = true;
    }
    if (!bad) Console.WriteLine("PASS: the demo consumes the package, not a project.");
    return bad ? 1 : 0;
}

int PackageChecks(string nupkg)
{
    if (!File.Exists(nupkg))
    {
        Console.Error.WriteLine("FAIL: no such package: " + nupkg);
        return 1;
    }

    var failed = false;
    void Fail(string message)
    {
        Console.Error.WriteLine("FAIL: " + message);
        failed = true;
    }

    var entries = ReadEntryNames(nupkg);
    foreach (var name in entries) Console.WriteLine(name);
    string[] required =
    [
        "analyzers/dotnet/cs/Neemle.Orm.Lite.Generator.dll", "lib/net10.0/Neemle.Orm.Lite.Runtime.dll",
        "buildTransitive/Neemle.Orm.Lite.props", "buildTransitive/Neemle.Orm.Lite.targets", "README.md",
    ];
    foreach (var name in required)
        if (!entries.Contains(name, StringComparer.Ordinal)) Fail("the package lacks " + name);
    if (failed) return 1;
    Console.WriteLine("PASS: the package carries the analyzer, the runtime and the build assets under one version.");

    var version = Path.GetFileNameWithoutExtension(nupkg)["Neemle.Orm.Lite.".Length..];
    var work = Path.Combine(Path.GetTempPath(), "orm-lite-pack-" + Guid.NewGuid().ToString("N"));
    var feed = Path.Combine(work, "feed");
    Directory.CreateDirectory(feed);
    File.Copy(nupkg, Path.Combine(feed, Path.GetFileName(nupkg)));
    var env = new Dictionary<string, string>
    {
        ["NUGET_PACKAGES"] = Path.Combine(work, "packages"),
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
    };
    var probes = Path.Combine(root, "tests", "PackConsumer");

    try
    {
        Console.WriteLine("==> consumer A: default reference gets the generators and the runtime");
        var a = NewConsumer("a", "", "Library");
        if (!Build(a, "a"))
        {
            Fail("A: the default consumer did not build");
        }
        else
        {
            foreach (var hint in new[] { "OrmLite.Entities.g.cs", "OrmLite.Migrations.g.cs" })
                if (Find(Path.Combine(a, "gen"), hint).Count == 0)
                    Fail("A: the default reference did not emit " + hint + " (analyzer unreachable)");
        }

        Console.WriteLine("==> consumer B: ExcludeAssets=analyzers gets the runtime and the actor setting initializer and emits nothing");
        var b = NewConsumer("b", "ExcludeAssets=\"analyzers\"", "Library");
        if (!Build(b, "b", "-p:OrmLiteActorSetting=probe.actor"))
        {
            Fail("B: the runtime-only consumer did not build");
        }
        else
        {
            if (Generated(b) != 0) Fail("B: ExcludeAssets=analyzers still emitted generated files");
            if (Find(Path.Combine(b, "obj"), "OrmLiteActorSetting.g.cs").Count == 0)
                Fail("B: the actor setting initializer did not reach the runtime-only consumer");
        }

        Console.WriteLine("==> consumer C: IncludeAssets=analyzers has no runtime and cannot compile");
        var c = NewConsumer("c", "IncludeAssets=\"analyzers\"", "Library");
        if (Build(c, "c"))
            Fail("C: an analyzer-only reference compiled although the runtime types are missing");
        else if (!System.Text.RegularExpressions.Regex.IsMatch(Log("c"), "error CS0(234|246|103)"))
            Fail("C: the analyzer-only consumer failed, but not on a missing runtime type");

        Console.WriteLine("==> consumer D: OrmLiteDefaultTenant reaches the generator from the package");
        var d = NewConsumer("d", "", "Library");
        File.Copy(Path.Combine(probes, "TenantProbe.cs"), Path.Combine(d, "TenantProbe.cs"));
        if (!Build(d, "d", "-p:OrmLiteDefaultTenant=OwnerId"))
        {
            Fail("D: the entity with the pinned tenant property did not build");
        }
        else if (!Find(Path.Combine(d, "gen"), "OrmLite.Entities.g.cs").Any(f => File.ReadAllText(f).Contains("\"owner_id\"", StringComparison.Ordinal)))
        {
            Fail("D: the pinned tenant column owner_id is absent from the emitted entities");
        }
        var d2 = NewConsumer("d2", "", "Library");
        File.Copy(Path.Combine(probes, "TenantProbe.cs"), Path.Combine(d2, "TenantProbe.cs"));
        if (Build(d2, "d2"))
            Fail("D: the same entity compiled without the property although the default tenant is not OwnerId");
        else if (!Log("d2").Contains("ORMLITE007", StringComparison.Ordinal))
            Fail("D: the unset consumer failed, but not on ORMLITE007");

        Console.WriteLine("==> consumer E: the actor setting initializer travels in buildTransitive and fires without a call");
        var e1 = NewConsumer("e1", "", "Exe");
        File.Copy(Path.Combine(probes, "Program.cs"), Path.Combine(e1, "Program.cs"));
        if (!Build(e1, "e1", "-p:OrmLiteActorSetting=probe.actor"))
            Fail("E: the pinned actor consumer did not build");
        else
            ExpectOutput(e1, "probe.actor", "the pinned consumer");
        var e2 = NewConsumer("e2", "", "Exe");
        File.Copy(Path.Combine(probes, "Program.cs"), Path.Combine(e2, "Program.cs"));
        if (!Build(e2, "e2"))
            Fail("E: the unpinned actor consumer did not build");
        else
            ExpectOutput(e2, "ormlite.actor", "the unpinned consumer");

        Console.WriteLine("==> consumer F: the nuspec declares no dependency on another library");
        if (ReadNuspec(nupkg).Contains("<dependency ", StringComparison.OrdinalIgnoreCase))
            Fail("F: the nuspec declares a package dependency");

        Console.WriteLine("==> consumer G: a project that only references consumer A gets the runtime and the initializer but no generator");
        var g = Path.Combine(work, "g");
        Directory.CreateDirectory(g);
        File.WriteAllText(Path.Combine(g, "Downstream.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                <CompilerGeneratedFilesOutputPath>$(MSBuildProjectDirectory)/gen</CompilerGeneratedFilesOutputPath>
                <RestoreLockedMode>false</RestoreLockedMode>
                <NuGetAudit>false</NuGetAudit>
              </PropertyGroup>
              <ItemGroup>
                <Compile Remove="gen/**" />
                <ProjectReference Include="../a/Consumer.csproj" />
              </ItemGroup>
            </Project>
            """);
        File.Copy(Path.Combine(a, "nuget.config"), Path.Combine(g, "nuget.config"));
        File.WriteAllText(Path.Combine(g, "Downstream.cs"),
            "public static class Downstream { public static System.Type Runtime => typeof(Neemle.Orm.Lite.OrmLiteSettings); }\n");
        if (!Build(g, "g", "-p:OrmLiteActorSetting=probe.actor"))
        {
            Fail("G: the downstream consumer did not build");
        }
        else
        {
            if (Generated(g) != 0) Fail("G: a project that only references a consumer of the package loaded the generators");
            if (Find(Path.Combine(g, "obj"), "OrmLiteActorSetting.g.cs").Count == 0)
                Fail("G: the actor setting initializer did not reach a project that never named the package");
        }

        void ExpectOutput(string dir, string expected, string who)
        {
            var (code, output) = Run(dir, ["dotnet", Path.Combine("bin", "Release", "net10.0", "Consumer.dll")]);
            var got = output.Replace("\r", "").Replace("\n", "");
            if (code != 0 || got != expected) Fail("E: " + who + " printed '" + got + "', expected " + expected);
        }

        string NewConsumer(string name, string assets, string outputType)
        {
            var dir = Path.Combine(work, name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Consumer.csproj"),
                File.ReadAllText(Path.Combine(probes, "Consumer.csproj.template"))
                    .Replace("__VERSION__", version).Replace("__ASSETS__", assets).Replace("__OUTPUT__", outputType));
            File.Copy(Path.Combine(probes, "Probe.cs"), Path.Combine(dir, "Probe.cs"));
            File.WriteAllText(Path.Combine(dir, "nuget.config"),
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <packageSources>\n    <clear />\n    <add key=\"fresh\" value=\""
                + feed + "\" />\n  </packageSources>\n</configuration>\n");
            return dir;
        }

        bool Build(string dir, string name, params string[] extra)
        {
            var (code, output) = Run(dir,
                ["dotnet", "build", "-c", "Release", "-v", "q", "--nologo", "-p:UseSharedCompilation=false", "-nodeReuse:false", .. extra]);
            File.WriteAllText(Path.Combine(work, name + ".log"), output);
            if (code != 0 && !failedBuildExpected(name))
            {
                var lines = output.Split('\n');
                Console.Error.WriteLine(string.Join('\n', lines.Skip(Math.Max(0, lines.Length - 30))));
            }
            return code == 0;
        }

        static bool failedBuildExpected(string name) => name is "c" or "d2";

        string Log(string name) => File.ReadAllText(Path.Combine(work, name + ".log"));
        int Generated(string dir) => Find(Path.Combine(dir, "gen"), "OrmLite.*.g.cs").Count;

        (int Code, string Output) Run(string dir, string[] command)
        {
            var info = new ProcessStartInfo(command[0])
            {
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in command.Skip(1)) info.ArgumentList.Add(arg);
            foreach (var (key, value) in env) info.Environment[key] = value;
            using var process = Process.Start(info)!;
            var stderr = process.StandardError.ReadToEndAsync();
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, stdout + stderr.Result);
        }
    }
    finally
    {
        try { Directory.Delete(work, recursive: true); } catch (IOException) { }
    }

    if (failed)
    {
        Console.Error.WriteLine("FAIL: PackConsumer matrix");
        return 1;
    }
    Console.WriteLine("PASS: PackConsumer matrix (default, runtime-only, analyzer-only, default tenant, actor setting, nuspec, downstream).");
    return 0;
}

static List<string> Find(string dir, string pattern) =>
    Directory.Exists(dir) ? Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories).ToList() : [];

static List<string> ReadEntryNames(string nupkg)
{
    using var zip = ZipFile.OpenRead(nupkg);
    return zip.Entries
        .Select(e => e.FullName)
        .Where(n => !n.EndsWith('/') && n is not "[Content_Types].xml" and not "_rels/.rels"
            && !n.StartsWith("package/services/", StringComparison.Ordinal) && !n.EndsWith(".nuspec", StringComparison.Ordinal))
        .Order(StringComparer.Ordinal)
        .ToList();
}

static string ReadNuspec(string nupkg)
{
    using var zip = ZipFile.OpenRead(nupkg);
    var entry = zip.Entries.First(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
    using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
    return reader.ReadToEnd();
}
