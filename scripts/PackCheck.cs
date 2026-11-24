#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: PackCheck.cs package <artifacts folder | core nupkg> | demo-guard | demo-guard-self-test");
    return 2;
}

var root = FindRoot();

switch (args[0])
{
    case "package" when args.Length == 2:
        return PackageChecks(Path.GetFullPath(args[1]));
    case "demo-guard":
        return DemoGuard(Path.Combine(root, "demo"), RootVersion(), Console.Out, Console.Error) ? 0 : 1;
    case "demo-guard-self-test":
        return DemoGuardSelfTest();
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

string RootVersion()
{
    var props = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
    return props.Descendants().First(e => e.Name.LocalName == "OrmLiteVersion").Value.Trim();
}

bool LeavesTheDemo(string project) =>
    project.Contains("..", StringComparison.Ordinal)
    || Path.IsPathRooted(project)
    || (project.StartsWith("$(", StringComparison.Ordinal) && !project.StartsWith("$(MSBuildThisFileDirectory)", StringComparison.Ordinal));

// The generator and core projects take this one property, so it is the version the generator builds against.
string ComposerVersion()
{
    var props = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
    return props.Descendants().Single(e => e.Name.LocalName == "OrmLiteComposerVersion").Value.Trim();
}

// The README rules live in scripts/ReadmeCheck.cs (a file-based app is one file); its output is this process's own.
int RunReadmeGuard(string artifacts)
{
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false };
    foreach (var argument in new[] { "run", Path.Combine("scripts", "ReadmeCheck.cs"), "--", "all", artifacts })
        start.ArgumentList.Add(argument);
    using var guard = Process.Start(start)!;
    guard.WaitForExit();
    return guard.ExitCode;
}

// The version stamps of the packed assemblies are read by scripts/PackVersions.cs, in the same way.
int RunVersionGuard(string artifacts, string version)
{
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, UseShellExecute = false };
    var script = Path.Combine("scripts", "PackVersions.cs");
    foreach (var argument in new[] { "run", script, "--", "check", artifacts, version })
        start.ArgumentList.Add(argument);
    using var guard = Process.Start(start)!;
    guard.WaitForExit();
    return guard.ExitCode;
}

bool DemoGuard(string demo, string version, TextWriter output, TextWriter errors)
{
    var bad = false;
    void Fail(string message)
    {
        errors.WriteLine("FAIL: " + message);
        bad = true;
    }

    var files = Directory.EnumerateFiles(demo, "*", SearchOption.AllDirectories)
        .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".demo-packages"))
        .Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            || f.EndsWith(".props", StringComparison.OrdinalIgnoreCase)
            || f.EndsWith(".targets", StringComparison.OrdinalIgnoreCase))
        .ToList();
    foreach (var stray in StrayConfigs(demo))
        Fail("the demo must not carry its own " + stray + ": it would change where the packages or the SDK come from");
    var packages = new List<(string Id, string? Version)>();
    foreach (var file in files)
    {
        var name = Path.GetRelativePath(demo, file);
        XDocument document;
        try
        {
            document = XDocument.Load(file);
        }
        catch (Exception error)
        {
            Fail("the demo build file " + name + " cannot be read as XML: " + error.Message);
            continue;
        }

        foreach (var element in document.Descendants())
        {
            var local = element.Name.LocalName;
            if (string.Equals(local, "ProjectReference", StringComparison.OrdinalIgnoreCase))
                Fail("the demo must reference packages, not a project: " + name + " has a ProjectReference");
            else if (string.Equals(local, "Reference", StringComparison.OrdinalIgnoreCase) || string.Equals(local, "HintPath", StringComparison.OrdinalIgnoreCase))
                Fail("the demo must reference packages, not an assembly: " + name + " has a " + local);
            else if (string.Equals(local, "Import", StringComparison.OrdinalIgnoreCase)
                && LeavesTheDemo(element.Attribute("Project")?.Value ?? ""))
                Fail("the demo must not import build files from outside the demo: " + name);
            else if (string.Equals(local, "PackageReference", StringComparison.OrdinalIgnoreCase))
                packages.Add((element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value ?? "", element.Attribute("Version")?.Value));
        }
    }

    var library = packages.Where(p => p.Id.StartsWith("Neemle.Orm.Lite", StringComparison.Ordinal)).ToList();
    if (!library.Any(p => p.Id == "Neemle.Orm.Lite"))
        Fail("the demo does not reference the Neemle.Orm.Lite package");
    if (!library.Any(p => p.Id is "Neemle.Orm.Lite.Postgres" or "Neemle.Orm.Lite.Sqlite"))
        Fail("the demo references no provider package (Neemle.Orm.Lite.Postgres or Neemle.Orm.Lite.Sqlite)");
    foreach (var (id, referenced) in library.Where(p => p.Version != version))
        Fail("the demo references " + id + " " + (referenced ?? "without a version") + ", not the packed version " + version);

    if (!bad) output.WriteLine("PASS: the demo consumes the packages at the packed version, and no project or assembly.");
    return !bad;
}

int DemoGuardSelfTest()
{
    var failed = false;
    var work = Path.Combine(Path.GetTempPath(), "orm-lite-guard-" + Guid.NewGuid().ToString("N"));
    const string Version = "9.9.9-test";
    try
    {
        string Csproj(string items) => "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>" + items + "</ItemGroup></Project>";
        const string Core = "<PackageReference Include=\"Neemle.Orm.Lite\" Version=\"" + Version + "\" />";
        const string Sqlite = "<PackageReference Include=\"Neemle.Orm.Lite.Sqlite\" Version=\"" + Version + "\" />";

        void Case(string name, bool expected, string csproj, string? extraName = null, string? extra = null)
        {
            var dir = Path.Combine(work, name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "OrmLiteDemo.csproj"), csproj);
            if (extraName is not null) File.WriteAllText(Path.Combine(dir, extraName), extra!);
            var got = DemoGuard(dir, Version, TextWriter.Null, TextWriter.Null);
            Console.WriteLine((got == expected ? "ok   " : "FAIL ") + "guard case " + name + ": expected " + (expected ? "pass" : "refuse") + ", got " + (got ? "pass" : "refuse"));
            if (got != expected) failed = true;
        }

        Case("packages-only", true, Csproj(Core + Sqlite));
        Case("project-reference", false, Csproj(Core + Sqlite + "<ProjectReference Include=\"../src/Neemle.Orm.Lite.Runtime/Neemle.Orm.Lite.Runtime.csproj\" />"));
        Case("project-reference-lowercase", false, Csproj(Core + Sqlite + "<projectreference Include=\"../src/X/X.csproj\" />"));
        Case("project-reference-in-targets", false, Csproj(Core + Sqlite), "Directory.Build.targets",
            "<Project><ItemGroup><ProjectReference Include=\"../src/X/X.csproj\" /></ItemGroup></Project>");
        Case("assembly-reference", false, Csproj(Core + Sqlite + "<Reference Include=\"X\"><HintPath>../src/X/bin/X.dll</HintPath></Reference>"));
        Case("import-from-outside", false, Csproj(Core + Sqlite), "Directory.Build.props", "<Project><Import Project=\"../Directory.Build.props\" /></Project>");
        Case("import-absolute-path", false, Csproj(Core + Sqlite), "Directory.Build.props", "<Project><Import Project=\"/elsewhere/x.props\" /></Project>");
        Case("import-from-property", false, Csproj(Core + Sqlite), "Directory.Build.props", "<Project><Import Project=\"$(SomeRoot)x.props\" /></Project>");
        Case("import-beside-the-file", true, Csproj(Core + Sqlite), "Directory.Build.props", "<Project><Import Project=\"$(MSBuildThisFileDirectory)x.props\" /></Project>");
        Case("no-core", false, Csproj(Sqlite));
        Case("no-provider", false, Csproj(Core));
        Case("other-version", false, Csproj(Core + "<PackageReference Include=\"Neemle.Orm.Lite.Sqlite\" Version=\"0.0.1\" />"));
        Case("no-version", false, Csproj(Core + "<PackageReference Include=\"Neemle.Orm.Lite.Sqlite\" />"));
        Case("not-xml", false, "this is not a project");
        Case("nuget-config-in-demo", false, Csproj(Core + Sqlite), "nuget.config", "<configuration />");
        Case("nuget-config-other-case", false, Csproj(Core + Sqlite), "NuGet.Config", "<configuration />");
        Case("global-json-in-demo", false, Csproj(Core + Sqlite), "global.json", "{}");
    }
    finally
    {
        try { Directory.Delete(work, recursive: true); } catch (IOException) { }
    }

    Console.WriteLine(failed ? "FAIL: the demo guard does not hold its own cases" : "PASS: the demo guard refuses every bypass it is meant to refuse.");
    return failed ? 1 : 0;
}

int PackageChecks(string location)
{
    var failed = false;
    void Fail(string message)
    {
        Console.Error.WriteLine("FAIL: " + message);
        failed = true;
    }

    var folder = Directory.Exists(location) ? location : Path.GetDirectoryName(location)!;
    var packages = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var id in new[] { "Neemle.Orm.Lite", "Neemle.Orm.Lite.Postgres", "Neemle.Orm.Lite.Sqlite" })
    {
        var found = Directory.GetFiles(folder, id + ".*.nupkg")
            .Where(f => Path.GetFileName(f).Length > id.Length + 1 && char.IsDigit(Path.GetFileName(f)[id.Length + 1]))
            .ToArray();
        if (found.Length != 1)
            Fail("expected exactly one " + id + " package in " + folder + ", found " + found.Length);
        else
            packages[id] = found[0];
    }
    if (failed) return 1;

    var specs = packages.ToDictionary(p => p.Key, p => XDocument.Parse(ReadNuspec(p.Value)), StringComparer.Ordinal);
    string Meta(string id, string name) => specs[id].Descendants().FirstOrDefault(e => e.Name.LocalName == name && e.Parent?.Name.LocalName == "metadata")?.Value.Trim() ?? "";
    var version = Meta("Neemle.Orm.Lite", "version");
    var rootVersion = RootVersion();
    if (version != rootVersion) Fail("the core package is version " + version + " but Directory.Build.props says " + rootVersion);
    foreach (var id in packages.Keys)
    {
        if (Meta(id, "id") != id) Fail(id + ": the nuspec id is " + Meta(id, "id"));
        if (Meta(id, "version") != version) Fail(id + ": version " + Meta(id, "version") + " differs from the core package version " + version);
        if (!Path.GetFileName(packages[id]).Equals(id + "." + Meta(id, "version") + ".nupkg", StringComparison.Ordinal))
            Fail(id + ": the file name " + Path.GetFileName(packages[id]) + " does not carry the nuspec version");
        foreach (var field in new[] { "authors", "description", "readme", "projectUrl", "tags", "copyright", "icon",
            "releaseNotes" })
            if (Meta(id, field).Length == 0) Fail(id + ": the nuspec has no " + field);
        var repository = specs[id].Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "repository" && e.Parent?.Name.LocalName == "metadata");
        if (repository?.Attribute("url")?.Value != "https://github.com/neemle/orm.lite")
            Fail(id + ": the nuspec repository url is " + (repository?.Attribute("url")?.Value ?? "missing"));
        var license = specs[id].Descendants().FirstOrDefault(e => e.Name.LocalName == "license" && e.Parent?.Name.LocalName == "metadata");
        if (license?.Attribute("type")?.Value != "expression" || license.Value.Trim() != "MIT")
            Fail(id + ": the nuspec must declare the license as type expression MIT, found " + (license is null ? "none" : license.Attribute("type")?.Value + " " + license.Value.Trim()));
    }

    var entries = packages.ToDictionary(p => p.Key, p => ReadEntryNames(p.Value), StringComparer.Ordinal);
    foreach (var (id, names) in entries)
    {
        Console.WriteLine("== " + id);
        foreach (var name in names) Console.WriteLine(name);
    }

    var required = new Dictionary<string, string[]>
    {
        ["Neemle.Orm.Lite"] =
        [
            "analyzers/dotnet/cs/Neemle.Orm.Lite.Generator.dll",
            "lib/net10.0/Neemle.Orm.Lite.Runtime.dll", "lib/net10.0/Neemle.Orm.Lite.Sql.dll",
            "buildTransitive/Neemle.Orm.Lite.props", "buildTransitive/Neemle.Orm.Lite.targets", "README.md", "LICENSE",
            "icon.png", "lib/net10.0/Neemle.Orm.Lite.Runtime.xml", "lib/net10.0/Neemle.Orm.Lite.Sql.xml",
        ],
        ["Neemle.Orm.Lite.Postgres"] =
        [
            "lib/net10.0/Neemle.Orm.Lite.Postgres.dll", "buildTransitive/Neemle.Orm.Lite.Postgres.props",
            "buildTransitive/ormlite.capabilities.postgres.txt", "README.md", "LICENSE",
            "icon.png", "lib/net10.0/Neemle.Orm.Lite.Postgres.xml",
        ],
        ["Neemle.Orm.Lite.Sqlite"] =
        [
            "lib/net10.0/Neemle.Orm.Lite.Sqlite.dll", "buildTransitive/Neemle.Orm.Lite.Sqlite.props",
            "buildTransitive/ormlite.capabilities.sqlite.txt", "README.md", "LICENSE",
            "icon.png", "lib/net10.0/Neemle.Orm.Lite.Sqlite.xml",
        ],
    };
    foreach (var (id, names) in required)
        foreach (var name in names)
            if (!entries[id].Contains(name, StringComparer.Ordinal)) Fail(id + " lacks " + name);
    foreach (var id in new[] { "Neemle.Orm.Lite.Postgres", "Neemle.Orm.Lite.Sqlite" })
        foreach (var name in entries[id].Where(n => n.Contains("Neemle.Orm.Lite.Runtime", StringComparison.Ordinal)
            || n.Contains("Neemle.Orm.Lite.Sql.dll", StringComparison.Ordinal) || n.StartsWith("analyzers/", StringComparison.Ordinal)))
            Fail(id + " carries " + name + ", which belongs to the core package only");
    if (failed) return 1;

    Console.WriteLine("==> the packed README is the consumer README: public links only, no private documentation");
    if (RunReadmeGuard(folder) != 0) Fail("scripts/ReadmeCheck.cs refused the packed README or a repository source");
    if (failed) return 1;

    Console.WriteLine("==> every packed assembly carries the package version in all three version stamps");
    if (RunVersionGuard(folder, version) != 0) Fail("scripts/PackVersions.cs refused the version of a packed assembly");
    if (failed) return 1;

    Console.WriteLine("==> the provider packages depend on the core package at exactly its version");
    foreach (var id in new[] { "Neemle.Orm.Lite.Postgres", "Neemle.Orm.Lite.Sqlite" })
    {
        var dependencies = specs[id].Descendants().Where(e => e.Name.LocalName == "dependency").ToList();
        var core = dependencies.Where(d => d.Attribute("id")?.Value == "Neemle.Orm.Lite").ToList();
        if (core.Count != 1)
            Fail(id + " must declare exactly one dependency on Neemle.Orm.Lite, found " + core.Count);
        else
        {
            if (core[0].Attribute("version")?.Value != "[" + version + "]")
                Fail(id + " depends on Neemle.Orm.Lite " + core[0].Attribute("version")?.Value + ", not exactly [" + version + "]");
            if (core[0].Attribute("exclude") is not null)
                Fail(id + " excludes assets of its Neemle.Orm.Lite dependency (" + core[0].Attribute("exclude")!.Value + "), so the generator and the build assets would not flow");
        }
        var driver = id.EndsWith(".Postgres", StringComparison.Ordinal) ? "Npgsql" : "Microsoft.Data.Sqlite";
        if (!dependencies.Any(d => d.Attribute("id")?.Value == driver)) Fail(id + " does not depend on " + driver);
    }
    Console.WriteLine("==> the core package takes Neemle.CSharpComposer as a package dependency and bundles no copy of it");
    var composerVersion = ComposerVersion();
    var coreDependencies = specs["Neemle.Orm.Lite"].Descendants().Where(e => e.Name.LocalName == "dependency").ToList();
    var composer = coreDependencies.Where(d => d.Attribute("id")?.Value == "Neemle.CSharpComposer").ToList();
    if (composer.Count != 1)
        Fail("the Neemle.Orm.Lite nuspec must declare exactly one dependency on Neemle.CSharpComposer, found " + composer.Count);
    else
    {
        if (composer[0].Attribute("version")?.Value != composerVersion)
            Fail("the Neemle.Orm.Lite nuspec depends on Neemle.CSharpComposer " + composer[0].Attribute("version")?.Value + ", but the generator project builds against " + composerVersion);
        var excluded = (composer[0].Attribute("exclude")?.Value ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (var asset in new[] { "Build", "Analyzers" })
            if (!excluded.Contains(asset, StringComparer.OrdinalIgnoreCase))
                Fail("the Neemle.CSharpComposer dependency does not exclude the " + asset + " assets, so they would flow to every consumer");
    }
    foreach (var other in coreDependencies.Where(d => d.Attribute("id")?.Value != "Neemle.CSharpComposer"))
        Fail("the Neemle.Orm.Lite nuspec declares an unexpected dependency on " + other.Attribute("id")?.Value);
    var coreAssemblies = entries["Neemle.Orm.Lite"].Where(n => n.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).ToArray();
    var expectedAssemblies = new[] { "analyzers/dotnet/cs/Neemle.Orm.Lite.Generator.dll", "lib/net10.0/Neemle.Orm.Lite.Runtime.dll", "lib/net10.0/Neemle.Orm.Lite.Sql.dll" };
    foreach (var stray in coreAssemblies.Except(expectedAssemblies, StringComparer.Ordinal))
        Fail("Neemle.Orm.Lite carries the assembly " + stray + ", which is not one of its own; dependencies are resolved by NuGet, not bundled");
    foreach (var (id, names) in entries)
        foreach (var name in names.Where(n => n.Contains("CSharpComposer", StringComparison.OrdinalIgnoreCase)))
            Fail(id + " carries " + name + "; Neemle.CSharpComposer is a dependency resolved by NuGet, not a bundled file");
    foreach (var id in new[] { "Neemle.Orm.Lite.Postgres", "Neemle.Orm.Lite.Sqlite" })
        if (specs[id].Descendants().Any(e => e.Name.LocalName == "dependency" && e.Attribute("id")?.Value == "Neemle.CSharpComposer"))
            Fail(id + " declares Neemle.CSharpComposer itself; it reaches the provider package through the core package");

    Console.WriteLine("==> each provider package ships its capability manifest and declares its provider to the build");
    foreach (var (id, provider, factory) in new[]
    {
        ("Neemle.Orm.Lite.Postgres", "postgres", "global::Neemle.Orm.Lite.Postgres.PostgresProviderFactory"),
        ("Neemle.Orm.Lite.Sqlite", "sqlite", "global::Neemle.Orm.Lite.Sqlite.SqliteProviderFactory"),
    })
    {
        var manifest = "ormlite.capabilities." + provider + ".txt";
        var source = File.ReadAllText(Path.Combine(root, "src", id, "assets", manifest));
        var packed = ReadEntry(packages[id], "buildTransitive/" + manifest);
        if (!string.Equals(source, packed, StringComparison.Ordinal))
            Fail(id + ": the packed " + manifest + " differs from src/" + id + "/assets/" + manifest);
        var props = XDocument.Parse(ReadEntry(packages[id], "buildTransitive/" + id + ".props"));
        var declared = props.Descendants().Where(e => e.Name.LocalName == "OrmLiteProviderPackage").ToList();
        if (declared.Count != 1
            || declared[0].Attribute("Include")?.Value != id
            || declared[0].Attribute("Provider")?.Value != provider
            || declared[0].Attribute("Factory")?.Value != factory)
            Fail(id + ": its props file must declare exactly the package " + id + " as provider '" + provider + "' with factory " + factory);
        var additional = props.Descendants().Where(e => e.Name.LocalName == "AdditionalFiles").Select(e => e.Attribute("Include")?.Value).ToList();
        if (additional.Count != 1 || additional[0] != "$(MSBuildThisFileDirectory)" + manifest)
            Fail(id + ": its props file must pass " + manifest + " to the compiler as an additional file");
    }
    if (failed) return 1;
    Console.WriteLine("PASS: three packages under one version, the providers pinned to the core, each with its manifest and provider declaration.");

    var work = Path.Combine(Path.GetTempPath(), "orm-lite-pack-" + Guid.NewGuid().ToString("N"));
    var feed = Path.Combine(work, "feed");
    Directory.CreateDirectory(feed);
    foreach (var package in packages.Values) File.Copy(package, Path.Combine(feed, Path.GetFileName(package)));
    var ambient = new[]
        {
            Environment.GetEnvironmentVariable("ORMLITE_PACK_PACKAGES"),
            Environment.GetEnvironmentVariable("NUGET_PACKAGES"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages"),
        }
        .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && Directory.Exists(p));
    var env = new Dictionary<string, string>
    {
        ["NUGET_PACKAGES"] = Path.Combine(work, "packages"),
        ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
    };
    var probes = Path.Combine(root, "tests", "PackConsumer");

    try
    {
        var composerCache = ambient is null ? null : Path.Combine(ambient, "neemle.csharpcomposer", composerVersion.ToLowerInvariant(), "neemle.csharpcomposer." + composerVersion.ToLowerInvariant() + ".nupkg");
        Console.WriteLine("==> every consumer restores Neemle.CSharpComposer " + composerVersion + " from the package cache, because the core package depends on it and does not carry it");
        if (composerCache is null || !File.Exists(composerCache))
        {
            Fail("the consumers restore Neemle.CSharpComposer " + composerVersion + " from the package cache, and " + (ambient ?? "no package cache")
                + " does not hold it (expected " + (composerCache ?? "neemle.csharpcomposer/" + composerVersion) + "); restore the solution first, or point ORMLITE_PACK_PACKAGES at a cache that does");
            return 1;
        }

        Console.WriteLine("==> consumer A: default reference gets the generators and the runtime");
        var a = NewConsumer("a", [Core("")], "Library", "Probe.cs");
        if (!Build(a, "a"))
        {
            Fail("A: the default consumer did not build");
        }
        else
        {
            foreach (var hint in new[] { "OrmLite.Entities.g.cs", "OrmLite.Migrations.g.cs" })
                if (Find(Path.Combine(a, "gen"), hint).Count == 0)
                    Fail("A: the default reference did not emit " + hint + " (analyzer unreachable)");
            ExpectComposerAnalyzer(a, "A", true);
        }

        Console.WriteLine("==> consumer B: ExcludeAssets=analyzers gets the runtime and the actor setting initializer and emits nothing");
        var b = NewConsumer("b", [Core("ExcludeAssets=\"analyzers\"")], "Library", "Probe.cs");
        if (!Build(b, "b", "-p:OrmLiteActorSetting=probe.actor"))
        {
            Fail("B: the runtime-only consumer did not build");
        }
        else
        {
            if (Generated(b) != 0) Fail("B: ExcludeAssets=analyzers still emitted generated files");
            ExpectComposerAnalyzer(b, "B", false);
            if (Find(Path.Combine(b, "obj"), "OrmLiteActorSetting.g.cs").Count == 0)
                Fail("B: the actor setting initializer did not reach the runtime-only consumer");
        }

        Console.WriteLine("==> consumer C: IncludeAssets=analyzers has no runtime and cannot compile");
        var c = NewConsumer("c", [Core("IncludeAssets=\"analyzers\"")], "Library", "Probe.cs");
        if (Build(c, "c"))
            Fail("C: an analyzer-only reference compiled although the runtime types are missing");
        else if (!Regex.IsMatch(Log("c"), "error CS0(234|246|103)"))
            Fail("C: the analyzer-only consumer failed, but not on a missing runtime type");

        Console.WriteLine("==> consumer D: OrmLiteDefaultTenant reaches the generator from the package");
        var d = NewConsumer("d", [Core("")], "Library", "Probe.cs", "TenantProbe.cs");
        if (!Build(d, "d", "-p:OrmLiteDefaultTenant=OwnerId"))
        {
            Fail("D: the entity with the pinned tenant property did not build");
        }
        else if (!Find(Path.Combine(d, "gen"), "OrmLite.Entities.g.cs").Any(f => File.ReadAllText(f).Contains("\"owner_id\"", StringComparison.Ordinal)))
        {
            Fail("D: the pinned tenant column owner_id is absent from the emitted entities");
        }
        var d2 = NewConsumer("d2", [Core("")], "Library", "Probe.cs", "TenantProbe.cs");
        if (Build(d2, "d2"))
            Fail("D: the same entity compiled without the property although the default tenant is not OwnerId");
        else if (!Log("d2").Contains("ORMLITE007", StringComparison.Ordinal))
            Fail("D: the unset consumer failed, but not on ORMLITE007");

        Console.WriteLine("==> consumer E: the actor setting initializer travels in buildTransitive and fires without a call");
        var e1 = NewConsumer("e1", [Core("")], "Exe", "Probe.cs", "Program.cs");
        if (!Build(e1, "e1", "-p:OrmLiteActorSetting=probe.actor"))
            Fail("E: the pinned actor consumer did not build");
        else
            ExpectOutput(e1, "probe.actor", "the pinned consumer");
        var e2 = NewConsumer("e2", [Core("")], "Exe", "Probe.cs", "Program.cs");
        if (!Build(e2, "e2"))
            Fail("E: the unpinned actor consumer did not build");
        else
            ExpectOutput(e2, "ormlite.actor", "the unpinned consumer");

        ExpectBadActorSettingsRefused();

        Console.WriteLine("==> consumer G: a project that only references consumer A gets the runtime and the initializer but no generator");
        var g = NewDownstream("g", "a", "ClassLib", "Library");
        File.WriteAllText(Path.Combine(g, "Downstream.cs"),
            "public static class Downstream { public static System.Type Runtime => typeof(Neemle.Orm.Lite.OrmLiteSettings); }\n");
        if (!Build(g, "g", "-p:OrmLiteActorSetting=probe.actor"))
        {
            Fail("G: the downstream consumer did not build");
        }
        else
        {
            if (Generated(g) != 0) Fail("G: a project that only references a consumer of the package loaded the generators");
            ExpectComposerAnalyzer(g, "G", false);
            if (Find(Path.Combine(g, "obj"), "OrmLiteActorSetting.g.cs").Count == 0)
                Fail("G: the actor setting initializer did not reach a project that never named the package");
        }

        RunPackagingShapeConsumers();

        Console.WriteLine("==> provider consumers need the third-party packages the provider packages depend on");
        if (ambient is null || !Directory.Exists(Path.Combine(ambient, "npgsql")) || !Directory.Exists(Path.Combine(ambient, "microsoft.data.sqlite")))
        {
            Fail("the provider consumers restore Npgsql and Microsoft.Data.Sqlite from the package cache, and "
                + (ambient ?? "no package cache") + " does not hold them; restore the solution first, or point ORMLITE_PACK_PACKAGES at a cache that does");
        }
        else
        {
            RunProviderConsumers();
        }

        void RunProviderConsumers()
        {
            string[] probe = ["Providers/Entities.cs", "Providers/ProviderProbe.cs", "Providers/Program.cs"];
            var sqliteOnly = new[] { Provider("Neemle.Orm.Lite.Sqlite") };
            var postgresOnly = new[] { Provider("Neemle.Orm.Lite.Postgres") };

            Console.WriteLine("==> consumer H: referencing only the Sqlite package is enough: generator, registration, default provider list, a working sqlite");
            var h = NewConsumer("h", sqliteOnly, "Exe", probe);
            if (!Build(h, "h"))
            {
                Fail("H: the sqlite-only consumer did not build");
            }
            else
            {
                ExpectGenerated(h, "H");
                ExpectComposerAnalyzer(h, "H", true);
                ExpectRegistration(h, "H", ["SqliteProviderFactory"], ["PostgresProviderFactory"]);
                ExpectProviderList(h, "H", ["sqlite"]);
                var report = ReadReport(h, "H");
                Expect(report, "H", "installed", "sqlite");
                ExpectStarts(report, "H", "sqlite", "ok rows=1 provider=sqlite global=1");
                ExpectNotInstalled(report, "H", "postgres", "Neemle.Orm.Lite.Postgres", "sqlite");
                ExpectUnset(report, "H", "sqlite");
            }

            Console.WriteLine("==> consumer I: referencing only the Postgres package registers postgres and nothing else");
            var i = NewConsumer("i", postgresOnly, "Exe", probe);
            if (!Build(i, "i"))
            {
                Fail("I: the postgres-only consumer did not build");
            }
            else
            {
                ExpectGenerated(i, "I");
                ExpectComposerAnalyzer(i, "I", true);
                ExpectRegistration(i, "I", ["PostgresProviderFactory"], ["SqliteProviderFactory"]);
                ExpectProviderList(i, "I", ["postgres"]);
                var report = ReadReport(i, "I");
                Expect(report, "I", "installed", "postgres");
                ExpectNotInstalled(report, "I", "sqlite", "Neemle.Orm.Lite.Sqlite", "postgres");
                ExpectStarts(report, "I", "postgres", "NpgsqlException");
                ExpectUnset(report, "I", "postgres");
            }

            Console.WriteLine("==> consumer J: both provider packages register both and the sqlite one still works");
            var j = NewConsumer("j", [.. sqliteOnly, .. postgresOnly], "Exe", probe);
            if (!Build(j, "j"))
            {
                Fail("J: the consumer of both providers did not build");
            }
            else
            {
                ExpectGenerated(j, "J");
                ExpectComposerAnalyzer(j, "J", true);
                ExpectRegistration(j, "J", ["SqliteProviderFactory", "PostgresProviderFactory"], []);
                ExpectProviderList(j, "J", ["postgres", "sqlite"]);
                var report = ReadReport(j, "J");
                Expect(report, "J", "installed", "postgres,sqlite");
                ExpectStarts(report, "J", "sqlite", "ok rows=1 provider=sqlite global=1");
                ExpectStarts(report, "J", "postgres", "NpgsqlException");
                ExpectUnset(report, "J", "postgres, sqlite");
            }

            Console.WriteLine("==> consumer K: the core package alone installs no provider, and says which package to add");
            var k = NewConsumer("k", [Core("")], "Exe", probe);
            if (!Build(k, "k"))
            {
                Fail("K: the core-only consumer did not build");
            }
            else
            {
                ExpectGenerated(k, "K");
                if (Find(Path.Combine(k, "obj"), "OrmLiteProviderRegistration.g.cs").Count != 0)
                    Fail("K: a registration was emitted although no provider package is referenced");
                ExpectProviderList(k, "K", []);
                var report = ReadReport(k, "K");
                Expect(report, "K", "installed", "");
                ExpectNotInstalled(report, "K", "sqlite", "Neemle.Orm.Lite.Sqlite", "none");
                ExpectNotInstalled(report, "K", "postgres", "Neemle.Orm.Lite.Postgres", "none");
                ExpectUnset(report, "K", "none");
            }

            Console.WriteLine("==> consumer L: the default provider list feeds the generator the manifests of the referenced providers");
            string[] lease = [.. probe, "Providers/LeaseProbe.cs"];
            var l1 = NewConsumer("l1", sqliteOnly, "Exe", lease);
            if (Build(l1, "l1"))
                Fail("L: a session lock lease compiled in a sqlite-only project although the sqlite manifest declares SessionLock None");
            else if (!Regex.IsMatch(Log("l1"), "ORMLITE016.*SessionLock.*'sqlite'"))
                Fail("L: the sqlite-only project failed, but not on ORMLITE016 naming SessionLock and 'sqlite'");
            var l2 = NewConsumer("l2", postgresOnly, "Exe", lease);
            if (!Build(l2, "l2"))
                Fail("L: a session lock lease did not compile in a postgres-only project although postgres supports it");
            var l3 = NewConsumer("l3", sqliteOnly, "Exe", lease);
            if (!Build(l3, "l3", "-p:OrmLiteProviders=postgres"))
                Fail("L: an explicit OrmLiteProviders did not win over the default provider list");
            else
                ExpectProviderList(l3, "L", ["postgres"]);

            Console.WriteLine("==> consumer M: OrmLiteRegisterProviders=false emits no registration, so the provider is not installed");
            var m = NewConsumer("m", sqliteOnly, "Exe", probe);
            if (!Build(m, "m", "-p:OrmLiteRegisterProviders=false"))
            {
                Fail("M: the opted-out consumer did not build");
            }
            else
            {
                if (Find(Path.Combine(m, "obj"), "OrmLiteProviderRegistration.g.cs").Count != 0)
                    Fail("M: a registration was emitted although OrmLiteRegisterProviders is false");
                var report = ReadReport(m, "M");
                Expect(report, "M", "installed", "");
                ExpectNotInstalled(report, "M", "sqlite", "Neemle.Orm.Lite.Sqlite", "none");
            }

            Console.WriteLine("==> consumer N: an application that only references a data library gets the registration and no generator");
            var nLib = NewConsumer("n", sqliteOnly, "Library", "Providers/Entities.cs", "Providers/ProviderProbe.cs");
            if (!Build(nLib, "n"))
            {
                Fail("N: the sqlite data library did not build");
            }
            else
            {
                var app = NewDownstream("napp", "n", "App", "Exe");
                File.Copy(Path.Combine(probes, "Providers", "Program.cs"), Path.Combine(app, "Program.cs"));
                if (!Build(app, "napp"))
                {
                    Fail("N: the application over the data library did not build");
                }
                else
                {
                    if (Generated(app) != 0) Fail("N: the application over the data library loaded the generators");
                    ExpectComposerAnalyzer(app, "N", false);
                    ExpectRegistration(app, "N", ["SqliteProviderFactory"], ["PostgresProviderFactory"]);
                    var report = ReadReport(app, "N", "App.dll");
                    Expect(report, "N", "installed", "sqlite");
                    ExpectStarts(report, "N", "sqlite", "ok rows=1 provider=sqlite global=1");
                }
            }

            ExpectVisualBasicConsumer();
        }

        void ExpectBadActorSettingsRefused()
        {
            Console.WriteLine("==> consumer F: an actor setting that breaks the runtime's rule fails the build "
                + "with ORMLITE032");
            foreach (var (name, value) in new[] { ("f1", "Bad"), ("f2", "app.Actor"), ("f3", "app.actor.more") })
            {
                var dir = NewConsumer(name, [Core("")], "Library", "Probe.cs");
                if (Build(dir, name, "-p:OrmLiteActorSetting=" + value))
                    Fail("F: '" + value + "' was accepted as an actor setting");
                else if (!Log(name).Contains("ORMLITE032", StringComparison.Ordinal))
                    Fail("F: '" + value + "' failed the build, but not on ORMLITE032");
            }
        }

        void ExpectVisualBasicConsumer()
        {
            Console.WriteLine("==> consumer V: a Visual Basic project gets no C# file from the package and a "
                + "warning for its actor setting");
            var dir = Path.Combine(work, "v");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Consumer.vbproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n"
                + "    <TargetFramework>net10.0</TargetFramework>\n    <OutputType>Library</OutputType>\n"
                + "    <NuGetAudit>false</NuGetAudit>\n  </PropertyGroup>\n"
                + "  <ItemGroup>\n    " + Provider("Neemle.Orm.Lite.Sqlite") + "\n  </ItemGroup>\n</Project>\n");
            File.WriteAllText(Path.Combine(dir, "Probe.vb"), "Public Module Probe\nEnd Module\n");
            File.WriteAllText(Path.Combine(dir, "nuget.config"), NuGetConfig());
            if (!Build(dir, "v", "-p:OrmLiteActorSetting=probe.actor", "-v:n"))
            {
                Fail("V: the Visual Basic consumer did not build");
                return;
            }
            if (Find(Path.Combine(dir, "obj"), "OrmLiteActorSetting.g.cs").Count != 0)
                Fail("V: a C# actor setting initializer was added to a Visual Basic project");
            if (Find(Path.Combine(dir, "obj"), "OrmLiteProviderRegistration.g.cs").Count != 0)
                Fail("V: a C# provider registration was added to a Visual Basic project");
            if (!Log("v").Contains("ORMLITE033", StringComparison.Ordinal))
                Fail("V: the Visual Basic project got no ORMLITE033 warning for its actor setting");
        }

        void RunPackagingShapeConsumers()
        {
            Console.WriteLine("==> consumer O: central package management, the reference carries no version");
            var o = NewConsumer("o", ["<PackageReference Include=\"Neemle.Orm.Lite\" />"], "Library", "Probe.cs");
            File.WriteAllText(Path.Combine(o, "Directory.Packages.props"),
                "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>"
                + "</PropertyGroup><ItemGroup><PackageVersion Include=\"Neemle.Orm.Lite\" Version=\"" + version
                + "\" /></ItemGroup></Project>\n");
            ExpectBuilt(o, "o", "O", "central package management");

            Console.WriteLine("==> consumer P: PrivateAssets=all on the reference changes nothing for the project");
            var p = NewConsumer("p", [Core("PrivateAssets=\"all\"")], "Library", "Probe.cs");
            ExpectBuilt(p, "p", "P", "PrivateAssets=all");

            Console.WriteLine("==> consumer Q: a lock file is written, and a locked-mode restore of it succeeds");
            var q = NewConsumer("q", [Core("")], "Library", "Probe.cs");
            if (!Build(q, "q", "-p:RestorePackagesWithLockFile=true"))
                Fail("Q: the consumer did not build while writing its lock file");
            else if (!Build(q, "q2", "-p:RestoreLockedMode=true"))
                Fail("Q: a locked-mode restore of the same consumer failed");
        }

        void ExpectBuilt(string dir, string name, string who, string what)
        {
            if (!Build(dir, name))
            {
                Fail(who + ": the consumer with " + what + " did not build");
                return;
            }
            ExpectGenerated(dir, who);
            ExpectComposerAnalyzer(dir, who, true);
        }

        void ExpectComposerAnalyzer(string dir, string who, bool expected)
        {
            var (code, output) = Run(dir, ["dotnet", "msbuild", "-t:Compile", "-p:Configuration=Release", "-p:UseSharedCompilation=false", "-nodeReuse:false", "-getItem:Analyzer"]);
            var start = output.IndexOf('{', StringComparison.Ordinal);
            if (code != 0 || start < 0)
            {
                Fail(who + ": the analyzer list could not be read (exit " + code + "): " + output.Trim());
                return;
            }
            using var document = JsonDocument.Parse(output[start..]);
            var analyzers = (document.RootElement.GetProperty("Items").TryGetProperty("Analyzer", out var listed) ? listed.EnumerateArray() : Enumerable.Empty<JsonElement>())
                .Select(item => item.GetProperty("Identity").GetString() ?? "").ToList();
            var found = analyzers.Any(a => IsFile(a, "neemle.csharpcomposer.dll"));
            if (found && !expected) Fail(who + ": Neemle.CSharpComposer reaches the compiler as an analyzer although the generator is not loaded in this project");
            if (!found && expected) Fail(who + ": Neemle.CSharpComposer does not reach the compiler as an analyzer, so the generator cannot load it: " + string.Join(';', analyzers));
        }

        void ExpectGenerated(string dir, string who)
        {
            if (Find(Path.Combine(dir, "gen"), "OrmLite.Entities.g.cs").Count == 0)
                Fail(who + ": the generator did not run although the provider package depends on the core package");
        }

        void ExpectRegistration(string dir, string who, string[] present, string[] absent)
        {
            var files = Find(Path.Combine(dir, "obj"), "OrmLiteProviderRegistration.g.cs");
            if (files.Count != 1)
            {
                Fail(who + ": expected one emitted provider registration, found " + files.Count);
                return;
            }
            var text = File.ReadAllText(files[0]);
            if (!text.Contains("[global::System.Runtime.CompilerServices.ModuleInitializer]", StringComparison.Ordinal))
                Fail(who + ": the emitted registration is not a module initializer");
            foreach (var name in present)
                if (!text.Contains("global::Neemle.Orm.Lite." + FactoryNamespace(name) + "." + name + ".Register();", StringComparison.Ordinal))
                    Fail(who + ": the emitted registration does not call " + name);
            foreach (var name in absent)
                if (text.Contains(name, StringComparison.Ordinal))
                    Fail(who + ": the emitted registration names " + name + ", which this project does not reference");
        }

        static string FactoryNamespace(string factory) => factory.Replace("ProviderFactory", "", StringComparison.Ordinal);

        void ExpectProviderList(string dir, string who, string[] expected)
        {
            var files = Find(Path.Combine(dir, "obj"), "*.GeneratedMSBuildEditorConfig.editorconfig");
            var line = files.SelectMany(File.ReadAllLines).FirstOrDefault(x => x.StartsWith("build_property.OrmLiteProviders", StringComparison.Ordinal));
            var got = (line?.Split('=', 2)[1] ?? "").Trim().Split(';', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToArray();
            if (!got.SequenceEqual(expected.Order(StringComparer.Ordinal)))
                Fail(who + ": the generator sees OrmLiteProviders '" + string.Join(';', got) + "', expected '" + string.Join(';', expected) + "'");
        }

        Dictionary<string, string> ReadReport(string dir, string who, string dll = "Consumer.dll")
        {
            var (code, output) = Run(dir, ["dotnet", Path.Combine("bin", "Release", "net10.0", dll)]);
            var report = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in output.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var at = line.IndexOf('=', StringComparison.Ordinal);
                if (at > 0) report[line[..at]] = line[(at + 1)..];
            }
            if (code != 0 || !report.ContainsKey("installed"))
                Fail(who + ": the consumer exited " + code + " without a report: " + output.Trim());
            return report;
        }

        void Expect(Dictionary<string, string> report, string who, string key, string expected)
        {
            if (!report.TryGetValue(key, out var got) || got != expected)
                Fail(who + ": " + key + " is '" + (got ?? "missing") + "', expected '" + expected + "'");
        }

        void ExpectStarts(Dictionary<string, string> report, string who, string key, string prefix)
        {
            if (!report.TryGetValue(key, out var got) || !got.StartsWith(prefix, StringComparison.Ordinal))
                Fail(who + ": " + key + " is '" + (got ?? "missing") + "', expected it to start with '" + prefix + "'");
        }

        void ExpectNotInstalled(Dictionary<string, string> report, string who, string provider, string package, string installed)
        {
            var expected = "InvalidOperationException: provider '" + provider + "' is not installed; add the package " + package
                + " to the application project (referencing the package registers the provider). Installed providers: " + installed + ".";
            Expect(report, who, provider, expected);
        }

        void ExpectUnset(Dictionary<string, string> report, string who, string installed)
        {
            var expected = "InvalidOperationException: no provider is configured: set OrmLite:Provider to one of the installed providers. Installed providers: "
                + installed + ". There is no default provider.";
            Expect(report, who, "unset", expected);
        }

        void ExpectOutput(string dir, string expected, string who)
        {
            var (code, output) = Run(dir, ["dotnet", Path.Combine("bin", "Release", "net10.0", "Consumer.dll")]);
            var got = output.Replace("\r", "").Replace("\n", "");
            if (code != 0 || got != expected) Fail("E: " + who + " printed '" + got + "', expected " + expected);
        }

        string Core(string assets) => "<PackageReference Include=\"Neemle.Orm.Lite\" Version=\"" + version + "\" " + assets + " />";

        string Provider(string id) => "<PackageReference Include=\"" + id + "\" Version=\"" + version + "\" />";

        string NuGetConfig() =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <packageSources>\n    <clear />\n    <add key=\"fresh\" value=\"" + feed + "\" />\n"
            + (ambient is null ? "" : "    <add key=\"ambient\" value=\"" + ambient + "\" />\n")
            + "  </packageSources>\n"
            + (ambient is null ? "" : "  <packageSourceMapping>\n    <packageSource key=\"fresh\">\n      <package pattern=\"Neemle.Orm.Lite*\" />\n    </packageSource>\n    <packageSource key=\"ambient\">\n      <package pattern=\"*\" />\n    </packageSource>\n  </packageSourceMapping>\n")
            + "</configuration>\n";

        string NewConsumer(string name, string[] references, string outputType, params string[] sources)
        {
            var dir = Path.Combine(work, name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Consumer.csproj"),
                File.ReadAllText(Path.Combine(probes, "Consumer.csproj.template"))
                    .Replace("__REFERENCES__", string.Join("\n    ", references)).Replace("__OUTPUT__", outputType));
            foreach (var source in sources) File.Copy(Path.Combine(probes, source), Path.Combine(dir, Path.GetFileName(source)));
            File.WriteAllText(Path.Combine(dir, "nuget.config"), NuGetConfig());
            return dir;
        }

        string NewDownstream(string name, string upstream, string assembly, string outputType)
        {
            var dir = Path.Combine(work, name);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, assembly + ".csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>{{outputType}}</OutputType>
                    <AssemblyName>{{assembly}}</AssemblyName>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                    <CompilerGeneratedFilesOutputPath>$(MSBuildProjectDirectory)/gen</CompilerGeneratedFilesOutputPath>
                    <NuGetAudit>false</NuGetAudit>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Remove="gen/**" />
                    <ProjectReference Include="../{{upstream}}/Consumer.csproj" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(dir, "nuget.config"), NuGetConfig());
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

        static bool failedBuildExpected(string name) => name is "c" or "d2" or "l1" or "f1" or "f2" or "f3";

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
    Console.WriteLine(
        "PASS: PackConsumer matrix (Composer from the cache, default, runtime-only, "
        + "analyzer-only, default tenant, actor setting, bad actor settings, Visual Basic, "
        + "downstream, central package management, PrivateAssets=all, lock file, "
        + "sqlite-only, postgres-only, both, core-only, default provider list, "
        + "opt-out, application over a data library).");
    return 0;
}

// The item identity is a path in the separator of the platform that built it: compare the file name only.
static bool IsFile(string identity, string file) =>
    string.Equals(Path.GetFileName(identity.Replace('\\', '/')), file, StringComparison.OrdinalIgnoreCase);

// A nuget.config or a global.json inside the demo would change where the packages or the SDK come from.
static List<string> StrayConfigs(string demo) =>
    Directory.EnumerateFiles(demo, "*", SearchOption.AllDirectories)
        .Where(f => !f.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj" or ".demo-packages"))
        .Where(f => Path.GetFileName(f).ToLowerInvariant() is "nuget.config" or "global.json")
        .Select(f => Path.GetRelativePath(demo, f)).ToList();

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

static string ReadEntry(string nupkg, string name)
{
    using var zip = ZipFile.OpenRead(nupkg);
    var entry = zip.GetEntry(name) ?? throw new FileNotFoundException(name + " is not in " + nupkg);
    using var reader = new StreamReader(entry.Open(), new UTF8Encoding(false));
    return reader.ReadToEnd();
}
