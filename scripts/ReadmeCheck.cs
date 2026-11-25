#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Files = (string Name, string Text)[];
using Case = (string Name, bool Clean, (string Name, string Text)[] Tracked, (string Name, string Text)[] Loose);

// ReadmeCheck guards what the NuGet packages and the public repository say to a consumer (ORML-38).
//   source             src/Packaging.props packs the consumer README, the consumer README source is sound, scripts/
//                      PackCheck.cs calls this tool with `all` and tests its exit code, the ci.yml package job runs
//                      self-test, source and PackCheck package in that order, and no TRACKED text file of the
//                      repository, this script included (git ls-files; an ignored or untracked file is not public),
//                      names documentation that only private tooling holds
//   package <folder>   the README.md inside each of the three packed packages is that consumer README
//   all <folder>       source, then package (this is what scripts/PackCheck.cs runs)
//   self-test          every rule refuses what it is meant to refuse, and accepts what it must accept
internal static class ReadmeCheck
{
    private const string RepoUrl = "https://github.com/neemle/orm.lite";
    private const string SourceReadme = "src/PackageReadme/README.md";
    private const string Include = "$(MSBuildThisFileDirectory)PackageReadme/README.md";
    private const RegexOptions Ci = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const RegexOptions Lines = Ci | RegexOptions.Multiline;

    // The words of the private documentation are assembled so that this public file never holds them verbatim.
    private const string Vibe = "Lease" + "Vibe";
    private const string Mcp = "M" + "CP";
    private const string Spec = "specifi" + "cation";

    private static readonly string[] Ids = ["Neemle.OrmLite", "Neemle.OrmLite.Postgres", "Neemle.OrmLite.Sqlite"];
    private static readonly string[] Headings = ["Status", "Install", "Quickstart", "Providers", "Documentation"];
    private static readonly string[] GitScope =
    [
        "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE", "GIT_OBJECT_DIRECTORY", "GIT_ALTERNATE_OBJECT_DIRECTORIES",
        "GIT_COMMON_DIR", "GIT_NAMESPACE", "GIT_PREFIX",
    ];
    private static readonly string NullDevice = OperatingSystem.IsWindows() ? "NUL" : "/dev/null";

    // Documentation that consumers of a package or readers of the public repository cannot reach.
    private static readonly (string What, Regex Pattern)[] PrivateDocs =
    [
        ("a section of the private documentation", new Regex(Spec + " section", Ci)),
        ("the private documentation", new Regex("project " + Spec, Ci)),
        ("the tool that holds the private documentation", new Regex(Vibe, Ci)),
        ("a server for the private documentation", new Regex(@"\b" + Mcp + @"\b", RegexOptions.CultureInvariant)),
    ];

    // What belongs to a contributor and means nothing, or the wrong thing, in a package README.
    private static readonly (string What, Regex Pattern)[] ContributorOnly =
    [
        ("a local feed step", new Regex(@"dotnet pack|nuget add source|nuget\.config|artifacts/", Ci)),
        ("a claim that the packages are unpublished", new Regex("not on a public feed", Ci)),
        ("contributor content", new Regex(@"^#+\s*(Repository layout|Build, test, pack)", Lines)),
        ("a path inside the repository", new Regex(@"(?<![\w/.:-])(src|tests|scripts|demo)/", Ci)),
    ];

    private static readonly Regex LinkTarget = new(@"\]\(\s*<?([^)\s>]+)|(?:href|src)\s*=\s*""([^""]+)""", Ci);
    private static readonly Regex AnyUrl = new(@"https?://[^\s)>""'\]]+", Ci);

    // The call itself, not a mention: a line without a comment before the call, the result tested, the `all` mode.
    private static readonly Regex HookCall =
        new(@"^[^/\r\n]*\bRunReadmeGuard\([^)]*\)\s*!=\s*0", RegexOptions.Multiline);
    private static readonly Regex HookArgs =
        new(@"^[^/\r\n]*""--""\s*,\s*""all""\s*,\s*artifacts", RegexOptions.Multiline);
    private static readonly string[] CiSteps =
        ["ReadmeCheck.cs -- self-test", "ReadmeCheck.cs -- source", "PackCheck.cs -- package"];

    private static int Main(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "";
        switch (command)
        {
            case "self-test" when args.Length == 1:
                return SelfTest();
            case "source" when args.Length == 1:
                return Report(command, SourceProblems(FindRoot()));
            case "package" when args.Length == 2:
                return Report(command, PackageProblems(FindRoot(), Path.GetFullPath(args[1])));
            case "all" when args.Length is 2 or 3:
                var root = FindRoot();
                return Report(command, [.. SourceProblems(root), .. PackageProblems(root, Path.GetFullPath(args[1]), args.Length == 3 ? args[2] : null)]);
            default:
                Console.Error.WriteLine("usage: ReadmeCheck.cs source | package <folder> | all <folder> [version] | self-test");
                return 2;
        }
    }

    private static string FindRoot()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Neemle.OrmLite.slnx"))) return d.FullName;
        throw new InvalidOperationException("Neemle.OrmLite.slnx not found above the current directory");
    }

    private static int Report(string what, List<string> problems)
    {
        foreach (var problem in problems) Console.Error.WriteLine("FAIL: " + problem);
        if (problems.Count == 0)
            Console.WriteLine("PASS: readme check '" + what + "': the consumer README ships, no private docs named.");
        return problems.Count == 0 ? 0 : 1;
    }

    // ---- the rules, over a text ----

    internal static List<string> ConsumerProblems(string text) =>
    [
        .. Forbidden(text, PrivateDocs), .. Forbidden(text, ContributorOnly),
        .. LinkProblems(text), .. MissingContent(text), .. LongLines(text),
    ];

    internal static List<string> PublicProblems(string text) => [.. Forbidden(text, PrivateDocs)];

    private static IEnumerable<string> Forbidden(string text, (string What, Regex Pattern)[] rules)
    {
        foreach (var (what, pattern) in rules)
            foreach (Match match in pattern.Matches(text))
                yield return "names " + what + " at line " + LineOf(text, match.Index) + ": " + match.Value.Trim();
    }

    private static int LineOf(string text, int index) => text.Take(index).Count(c => c == '\n') + 1;

    private static IEnumerable<string> LinkProblems(string text)
    {
        foreach (Match match in LinkTarget.Matches(text))
        {
            var target = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (!target.StartsWith('#') && !IsPublic(target))
                yield return "links to " + target + ", which is not a public URL of the repository";
        }
        foreach (Match match in AnyUrl.Matches(text))
            if (!IsPublic(match.Value))
                yield return "names the URL " + match.Value + ", which is not a public URL of the repository";
    }

    private static bool IsPublic(string url)
    {
        if (!url.StartsWith(RepoUrl, StringComparison.Ordinal)) return false;
        var rest = url[RepoUrl.Length..];
        return rest.Length == 0 || rest[0] is '/' or '#' or '?';
    }

    private static IEnumerable<string> MissingContent(string text)
    {
        foreach (var heading in Headings)
            if (!Regex.IsMatch(text, @"^##\s+" + heading + @"\b", RegexOptions.Multiline))
                yield return "has no section '## " + heading + "'";
        foreach (var id in Ids)
            if (!text.Contains("`" + id + "`", StringComparison.Ordinal))
                yield return "does not name the package " + id;
        if (!text.Contains("dotnet add package", StringComparison.Ordinal))
            yield return "has no 'dotnet add package' line";
        if (!text.Contains(RepoUrl, StringComparison.Ordinal))
            yield return "links to no public URL of the repository";
    }

    private static IEnumerable<string> LongLines(string text) =>
        text.Split('\n').Select((line, i) => (Number: i + 1, Length: line.TrimEnd('\r').Length))
            .Where(l => l.Length > 120)
            .Select(l => "line " + l.Number + " is " + l.Length + " characters, the limit is 120");

    // ---- the repository ----

    private static List<string> SourceProblems(string root)
    {
        var tracked = GitFiles(root);
        var problems = WiringProblems(root);
        problems.AddRange(ProjectProblems(root, tracked));
        problems.AddRange(HookProblems(root));
        problems.AddRange(CiProblems(root));
        foreach (var name in tracked)
            foreach (var problem in PublicProblems(ReadText(root, name) ?? ""))
                problems.Add(name + " " + problem);
        var readme = Path.Combine(root, SourceReadme);
        if (File.Exists(readme))
            problems.AddRange(ConsumerProblems(File.ReadAllText(readme)).Select(p => SourceReadme + " " + p));
        return problems;
    }

    // The public tree is what git tracks: an ignored or untracked file (the agent files of a governed checkout)
    // is not published, so it is neither scanned nor a reason to refuse.
    private static List<string> GitFiles(string root) =>
        [.. Git(root, "ls-files", "-z").Split('\0', StringSplitOptions.RemoveEmptyEntries)];

    // Every tracked file is read, whatever its extension; one with a NUL byte near the start is binary and skipped.
    private static string? ReadText(string root, string name)
    {
        var path = Path.Combine(root, name);
        if (!File.Exists(path)) return null;
        var bytes = File.ReadAllBytes(path);
        var binary = Array.IndexOf(bytes, (byte)0, 0, Math.Min(bytes.Length, 8000)) >= 0;
        return binary ? null : Encoding.UTF8.GetString(bytes);
    }

    // The throwaway repositories of the self-test must never reach the caller's repository or configuration, so a
    // run from a git hook (GIT_DIR, GIT_INDEX_FILE) or under a user's global ignore file behaves as a plain run.
    private static ProcessStartInfo GitStart(string root, string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
        };
        foreach (var name in GitScope) start.Environment.Remove(name);
        start.Environment["GIT_CONFIG_GLOBAL"] = NullDevice;
        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        string[] scope = ["-c", "core.excludesFile=" + NullDevice, "-c", "safe.directory=" + root];
        foreach (var argument in scope.Concat(arguments)) start.ArgumentList.Add(argument);
        return start;
    }

    private static string Git(string root, params string[] arguments)
    {
        using var git = Process.Start(GitStart(root, arguments))!;
        var errors = git.StandardError.ReadToEndAsync();
        var output = git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0)
            throw new InvalidOperationException("git " + arguments[0] + " failed in " + root + ": " + errors.Result);
        return output;
    }

    // Without its call to this tool PackCheck would pass a package whose README nobody looked at: the call must be
    // there as code, with `all` and a tested exit code, not only named in a comment or a string.
    private static IEnumerable<string> HookProblems(string root)
    {
        var pack = Path.Combine(root, "scripts", "PackCheck.cs");
        var text = File.Exists(pack) ? File.ReadAllText(pack) : "";
        if (!HookCall.IsMatch(text))
            yield return "scripts/PackCheck.cs does not call RunReadmeGuard(...) and test its exit code with != 0";
        if (!HookArgs.IsMatch(text))
            yield return "scripts/PackCheck.cs does not start scripts/ReadmeCheck.cs with the 'all' argument";
    }

    // The self-test must run before the check it guards, and both before PackCheck package (which runs `all`).
    private static IEnumerable<string> CiProblems(string root)
    {
        var path = Path.Combine(root, ".github", "workflows", "ci.yml");
        var job = File.Exists(path) ? PackageJob(File.ReadAllText(path)) : "";
        var at = CiSteps.Select(step => job.IndexOf("run: dotnet run scripts/" + step, StringComparison.Ordinal))
            .ToList();
        if (at.Contains(-1) || !at.SequenceEqual(at.Order()))
            yield return ".github/workflows/ci.yml job package must run ReadmeCheck self-test, ReadmeCheck source and "
                + "PackCheck package, in that order";
    }

    private static string PackageJob(string yaml)
    {
        var job = Regex.Match(yaml, @"^  package:[ \t]*$(.*?)(?=^  \S[^\n]*:[ \t]*$|\z)",
            RegexOptions.Multiline | RegexOptions.Singleline);
        return job.Success ? job.Groups[1].Value : "";
    }

    private static List<string> WiringProblems(string root)
    {
        var problems = new List<string>();
        if (!File.Exists(Path.Combine(root, SourceReadme))) problems.Add(SourceReadme + " does not exist");
        var props = XDocument.Load(Path.Combine(root, "src", "Packaging.props"));
        var file = props.Descendants().FirstOrDefault(e => e.Name.LocalName == "PackageReadmeFile")?.Value.Trim();
        if (file != "README.md")
            problems.Add("src/Packaging.props: PackageReadmeFile must be README.md, found " + (file ?? "none"));
        var items = props.Descendants().Where(IsReadmeItem).ToList();
        if (items.Count != 1 || !PacksSource(items[0]))
            problems.Add("src/Packaging.props must pack exactly " + Include + " at the package root, "
                + "as the only README");
        return problems;
    }

    private static bool IsReadmeItem(XElement e) =>
        e.Name.LocalName is "None" or "Content"
        && (e.Attribute("Include")?.Value ?? "").EndsWith("README.md", StringComparison.OrdinalIgnoreCase);

    private static bool PacksSource(XElement e) =>
        e.Attribute("Include")?.Value == Include
        && e.Attribute("PackagePath")?.Value == "/"
        && e.Attribute("Pack")?.Value == "true";

    private static IEnumerable<string> ProjectProblems(string root, List<string> tracked) =>
        tracked.Where(n => n.StartsWith("src/", StringComparison.Ordinal))
            .Where(n => n.EndsWith(".csproj", StringComparison.Ordinal) && File.Exists(Path.Combine(root, n)))
            .Where(n => File.ReadAllText(Path.Combine(root, n)).Contains("README", StringComparison.OrdinalIgnoreCase))
            .Select(n => n + " names a README itself; Packaging.props packs it");

    // ---- the packages ----

    private static List<string> PackageProblems(string root, string folder, string? version = null)
    {
        var problems = new List<string>();
        var source = File.ReadAllText(Path.Combine(root, SourceReadme));
        foreach (var id in Ids)
        {
            var found = version is null
                ? Directory.GetFiles(folder, id + ".*.nupkg")
                    .Where(f => char.IsDigit(Path.GetFileName(f)[id.Length + 1])).ToArray()
                : Directory.GetFiles(folder, id + "." + version + ".nupkg");
            if (found.Length != 1)
                problems.Add("expected exactly one " + id + " package in " + folder + ", found " + found.Length);
            else
                problems.AddRange(OnePackage(found[0], source).Select(p => id + ": " + p));
        }
        return problems;
    }

    private static List<string> OnePackage(string nupkg, string source)
    {
        var readme = ReadEntry(nupkg, "README.md");
        if (readme is null) return ["the package has no README.md at its root"];
        var problems = new List<string>();
        if (NuspecReadme(nupkg) != "README.md") problems.Add("the nuspec does not name README.md as its readme");
        if (readme != source) problems.Add("the packed README.md is not " + SourceReadme);
        problems.AddRange(ConsumerProblems(readme).Select(p => "the packed README.md " + p));
        return problems;
    }

    private static string? ReadEntry(string nupkg, string name)
    {
        using var zip = ZipFile.OpenRead(nupkg);
        var entry = zip.GetEntry(name);
        if (entry is null) return null;
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    private static string? NuspecReadme(string nupkg)
    {
        using var zip = ZipFile.OpenRead(nupkg);
        var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal));
        if (entry is null) return null;
        using var stream = entry.Open();
        var spec = XDocument.Load(stream);
        return spec.Descendants().FirstOrDefault(e => e.Name.LocalName == "readme")?.Value.Trim();
    }

    // ---- self-test ----

    private const string Good = """
        # Neemle.OrmLite

        ## Status

        Pre-release.

        ## Install

        `Neemle.OrmLite`, `Neemle.OrmLite.Postgres` and `Neemle.OrmLite.Sqlite`.

            dotnet add package Neemle.OrmLite.Sqlite --prerelease

        ## Quickstart

        ## Providers

        ## Documentation

        See [the repository](https://github.com/neemle/orm.lite#readme) and
        [the demo](https://github.com/neemle/orm.lite/tree/HEAD/demo), or [Install](#install).
        """;

    private static int SelfTest()
    {
        var failed = false;
        foreach (var (name, text, consumer, clean) in TextCases())
        {
            var got = (consumer ? ConsumerProblems(text) : PublicProblems(text)).Count == 0;
            failed |= Expect("text case " + name, clean, got);
        }
        failed |= ScriptSelfTest();
        var work = Path.Combine(Path.GetTempPath(), "orm-lite-readme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            failed |= PackageSelfTest(work);
            failed |= TreeSelfTest(work);
        }
        finally
        {
            try { Cleanup(work); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        Console.WriteLine(failed ? "FAIL: the README guard does not hold its own cases"
            : "PASS: the README guard holds its own cases.");
        return failed ? 1 : 0;
    }

    // This public script must not spell the words it hides: its own text is scanned like every tracked file.
    private static bool ScriptSelfTest()
    {
        var script = File.ReadAllText(Path.Combine(FindRoot(), "scripts", "ReadmeCheck.cs"));
        return Expect("script text", true, PublicProblems(script).Count == 0);
    }

    // Git leaves read-only object files that Windows will not delete until the attribute is cleared.
    private static void Cleanup(string work)
    {
        foreach (var file in Directory.EnumerateFiles(work, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(work, recursive: true);
    }

    private static bool PackageSelfTest(string work)
    {
        var failed = false;
        foreach (var (name, readme, nuspec, clean) in PackageCases())
        {
            var packed = MakePackage(work, name, readme, nuspec);
            failed |= Expect("package case " + name, clean, OnePackage(packed, Good).Count == 0);
        }
        return failed;
    }

    // Each case is a throwaway git repository: the tracked files are added to the index, the loose ones are written
    // after it, so they are untracked (or ignored, or deleted) exactly as in a governed checkout. GIT_DIR points at
    // nothing while the cases run: Git() must drop it, or the cases would reach for a repository that is not theirs.
    private static bool TreeSelfTest(string work)
    {
        var failed = false;
        var saved = Environment.GetEnvironmentVariable("GIT_DIR");
        Environment.SetEnvironmentVariable("GIT_DIR", Path.Combine(work, "no-such-git-dir"));
        try
        {
            foreach (var (name, clean, tracked, loose) in AllTreeCases())
            {
                var root = Path.Combine(work, "tree-" + name);
                WriteFiles(root, tracked);
                Git(root, "init", "-q");
                Git(root, "add", "-A");
                WriteFiles(root, loose);
                failed |= Expect("tree case " + name, clean, SourceProblems(root).Count == 0);
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_DIR", saved);
        }
        return failed;
    }

    // The text Gone deletes the file: a tracked file that is missing from the work tree.
    private static void WriteFiles(string root, Files files)
    {
        foreach (var (name, text) in files)
        {
            var full = Path.Combine(root, name);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            if (text == Gone) File.Delete(full);
            else File.WriteAllText(full, text);
        }
    }

    private static bool Expect(string name, bool clean, bool got)
    {
        Console.WriteLine((got == clean ? "ok   " : "FAIL ") + name + ": expected " + (clean ? "clean" : "refused")
            + ", got " + (got ? "clean" : "refused"));
        return got != clean;
    }

    private static (string Name, string Text, bool Consumer, bool Clean)[] TextCases()
    {
        static string Add(string extra) => Good + "\n\n" + extra + "\n";
        return
        [
            ("good", Good, true, true),
            ("anchors-and-repo-links", Add("[a](#status) <https://github.com/neemle/orm.lite/issues>"), true, true),
            ("relative-link", Add("[demo](demo/README.md)"), true, false),
            ("relative-image", Add("<img src=\"logo.png\">"), true, false),
            ("foreign-host", Add("[docs](https://example.com/docs)"), true, false),
            ("plain-http", Add("[repo](http://github.com/neemle/orm.lite)"), true, false),
            ("lookalike-repository", Add("https://github.com/neemle/orm.lite.evil/x"), true, false),
            ("local-feed-pack", Add("dotnet pack Neemle.OrmLite.slnx -c Release"), true, false),
            ("local-feed-source", Add("dotnet nuget add source /a/artifacts"), true, false),
            ("unpublished-claim", Add("The packages are not on a public feed yet."), true, false),
            ("section-reference", Add("See the " + Spec + " section \"Examples\"."), true, false),
            ("project-document", Add("kept in the project " + Spec), true, false),
            ("tool-name", Add("on the " + Vibe + " server"), true, false),
            ("server-name", Add("an " + Mcp + " server"), true, false),
            ("contributor-heading", Add("## Repository layout"), true, false),
            ("repository-path", Add("see src/Packaging.props"), true, false),
            ("no-install-section", Good.Replace("## Install", "## Setup"), true, false),
            ("no-package-name", Good.Replace("`Neemle.OrmLite.Postgres`", "Postgres"), true, false),
            ("long-line", Add(new string('x', 121)), true, false),
            ("public-section-reference", Add("the " + Spec + " section \"Examples\""), false, false),
            ("public-history-document", Add("the " + Spec + " a trigger was built from"), false, true),
        ];
    }

    private static (string Name, string? Readme, bool Nuspec, bool Clean)[] PackageCases() =>
    [
        ("packed-source", Good, true, true),
        ("no-readme", null, true, false),
        ("nuspec-without-readme", Good, false, false),
        ("developer-readme", Good + "\n## Build, test, pack, demo\n", true, false),
        ("not-the-source", Good + "\nextra\n", true, false),
    ];

    // ---- the tree cases: a fixture is the tracked files of a small repository (see TreeSelfTest) ----

    private const string Item = "<None Include=\"" + Include + "\" Pack=\"true\" PackagePath=\"/\" />";
    private const string Gone = "\0gone";
    private const string Private = Vibe + ", an " + Mcp + " server";
    private const string Hook = "if (RunReadmeGuard(folder) != 0) Fail(\"x\");\n"
        + "foreach (var a in new[] { \"run\", \"--\", \"all\", artifacts }) { }";
    private static readonly string GoodCi = CiWith(CiSteps[0], CiSteps[1], CiSteps[2] + " artifacts");
    private static readonly (string, string) Csproj =
        ("src/X/X.csproj", "<Project><None Include=\"README.md\" /></Project>");

    private static string PropsWith(string items) =>
        "<Project><PropertyGroup><PackageReadmeFile>README.md</PackageReadmeFile></PropertyGroup>"
        + "<ItemGroup>" + items + "</ItemGroup></Project>";

    private static (string, string) Props(string items) => ("src/Packaging.props", PropsWith(items));

    private static string CiWith(params string[] steps) =>
        "jobs:\n  package:\n    steps:\n"
        + string.Concat(steps.Select(step => "      - run: dotnet run scripts/" + step + "\n"))
        + "  demo:\n    steps: []\n";

    private static (string, string)[] Basis(string pack = Hook, string? ci = null) =>
        [(SourceReadme, Good), ("scripts/PackCheck.cs", pack), (".github/workflows/ci.yml", ci ?? GoodCi)];

    private static Case C(string name, bool clean, Files tracked, Files? loose = null) =>
        (name, clean, tracked, loose ?? Array.Empty<(string, string)>());

    private static Case[] AllTreeCases() => [.. TreeCases(), .. RuleCases()];

    private static Case[] TreeCases()
    {
        const string Old =
            "<None Include=\"$(MSBuildThisFileDirectory)../README.md\" Pack=\"true\" PackagePath=\"/\" />";
        const string Content = "<Content Include=\"../README.md\" Pack=\"true\" PackagePath=\"/\" />";
        var wrongFile = ("src/Packaging.props", PropsWith(Item).Replace("README.md</", "docs.md</"));
        return
        [
            C("good", true, [.. Basis(), Props(Item)]),
            C("old-readme-item", false, [.. Basis(), Props(Old)]),
            C("content-item", false, [.. Basis(), Props(Item + Content)]),
            C("wrong-readme-file", false, [.. Basis(), wrongFile]),
            C("csproj-names-readme", false, [.. Basis(), Props(Item), Csproj]),
            C("tracked-private-reference", false, [.. Basis(), Props(Item), ("docs/a.md", Private)]),
            C("untracked-private-reference", true, [.. Basis(), Props(Item)], [("AGENTS.md", Private)]),
            C("ignored-private-reference", true, [.. Basis(), Props(Item), (".gitignore", "AGENTS.md\n")],
                [("AGENTS.md", Private)]),
            C("no-source-readme", false, [.. Basis().Skip(1), Props(Item)]),
            C("hook-removed", false, [.. Basis("// nothing"), Props(Item)]),
        ];
    }

    // Wiring fields, the scan scope (every tracked file, not binary ones), the hook call and the ci.yml order.
    private static Case[] RuleCases() =>
    [
        C("packagepath-wrong", false, [.. Basis(), Props(Item.Replace("PackagePath=\"/", "PackagePath=\"docs/"))]),
        C("pack-false", false, [.. Basis(), Props(Item.Replace("Pack=\"true\"", "Pack=\"false\""))]),
        C("deleted-csproj", true, [.. Basis(), Props(Item), Csproj], [(Csproj.Item1, Gone)]),
        C("golden-private-reference", false, [.. Basis(), Props(Item), ("corpus/x.golden", Private)]),
        C("license-private-reference", false, [.. Basis(), Props(Item), ("LICENSE", Private)]),
        C("binary-skipped", true, [.. Basis(), Props(Item), ("data/x.bin", "\0" + Private)]),
        C("script-scanned", false, [.. Basis(), Props(Item), ("scripts/ReadmeCheck.cs", Private)]),
        C("hook-comment-only", false, [.. Basis("// " + Hook.Replace("\n", "\n// ")), Props(Item)]),
        C("hook-wrong-argument", false, [.. Basis(Hook.Replace("\"all\"", "\"self-test\"")), Props(Item)]),
        C("hook-result-untested", false, [.. Basis(Hook.Replace(" != 0", "")), Props(Item)]),
        C("ci-self-test-after-package", false, [.. Basis(ci: Ci3(1, 2, 0)), Props(Item)]),
        C("ci-source-step-missing", false, [.. Basis(ci: CiWith(CiSteps[0], CiSteps[2])), Props(Item)]),
        C("ci-source-before-self-test", false, [.. Basis(ci: Ci3(1, 0, 2)), Props(Item)]),
    ];

    private static string Ci3(int a, int b, int c) => CiWith(CiSteps[a], CiSteps[b], CiSteps[c]);

    private static string MakePackage(string folder, string name, string? readme, bool nuspec)
    {
        var path = Path.Combine(folder, name + ".nupkg");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var readmeElement = nuspec ? "<readme>README.md</readme>" : "";
        AddEntry(zip, "x.nuspec", "<package><metadata>" + readmeElement + "</metadata></package>");
        if (readme is not null) AddEntry(zip, "README.md", readme);
        return path;
    }

    private static void AddEntry(ZipArchive zip, string name, string text)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(text);
    }
}
