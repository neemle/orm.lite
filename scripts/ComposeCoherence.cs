#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
using System.Text.RegularExpressions;

// ComposeCoherence keeps the PostgreSQL image major and the host ports written once (ORML-79). The compose files are
// the source: the image default and the host port default of tests/compose.yaml, the image and the host port of
// demo/compose.yaml. Everything else that repeats a value (ci.yml, the READMEs, the comments of the compose files)
// must say the same, so a change to a source that misses a repetition fails here instead of in a hosted run.
//   check          the container port of both servers is 5432, both servers use one image major, the two host ports
//                  differ, and every repetition of the major and of each port equals its source
//   self-test      a passing fixture and one mutated negative per assertion
// scripts/StepShape.cs requires the two steps of the ci.yml test job that run this script.
internal static class ComposeCoherence
{
    private const RegexOptions Opt = RegexOptions.CultureInvariant | RegexOptions.Multiline;

    private static readonly string[] Paths =
        [".github/workflows/ci.yml", "tests/compose.yaml", "demo/compose.yaml", "README.md", "demo/README.md"];

    private static readonly Regex TestImage =
        new(@"^\s*image:\s*\$\{ORMLITE_TEST_PG_IMAGE:-neemle/pg\.lite:(?<major>\d+)\}", Opt);
    private static readonly Regex TestPort =
        new(@"""127\.0\.0\.1:\$\{ORMLITE_TEST_PG_PORT:-(?<host>\d+)\}:(?<container>\d+)""", Opt);
    private static readonly Regex DemoImage = new(@"^\s*image:\s*neemle/pg\.lite:(?<major>\d+)\s*$", Opt);
    private static readonly Regex DemoPort = new(@"""127\.0\.0\.1:(?<host>\d+):(?<container>\d+)""", Opt);

    // The repetitions: the image tag, the port of a test connection string or of the port variable, and the demo port.
    private static readonly Regex Major = new(@"neemle/pg\.lite:(\d+)", Opt);
    private static readonly Regex TestUse =
        new(@"Port=(\d+);Username|ORMLITE_TEST_PG_PORT\W+(?:default\s+)?(\d+)", Opt);
    private static readonly Regex DemoUse = new(@"(?:OrmLite__Port[=:]\s*""?|127\.0\.0\.1:)(\d+)", Opt);

    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "self-test") return SelfTest();
        if (args.Length == 1 && args[0] == "check") return Check();
        Console.Error.WriteLine("usage: ComposeCoherence.cs check | self-test");
        return 2;
    }

    private static string Root()
    {
        for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Neemle.Orm.Lite.slnx"))) return d.FullName;
        throw new InvalidOperationException("Neemle.Orm.Lite.slnx not found above the current directory");
    }

    private static int Check()
    {
        var root = Root();
        var missing = Paths.Where(p => !File.Exists(Path.Combine(root, p))).ToList();
        foreach (var path in missing) Console.Error.WriteLine("FAIL: the file " + path + " does not exist");
        if (missing.Count > 0) return 1;
        var texts = Paths.ToDictionary(p => p, p => File.ReadAllText(Path.Combine(root, p)));
        var fails = Judge(texts);
        foreach (var message in fails) Console.Error.WriteLine("FAIL: " + message);
        if (fails.Count == 0) Console.WriteLine("PASS: the image major and the host ports agree everywhere.");
        return fails.Count == 0 ? 0 : 1;
    }

    private static Match? Pick(IReadOnlyDictionary<string, string> t, string file, Regex pattern, string what,
        List<string> f)
    {
        var match = pattern.Match(t[file]);
        if (!match.Success) f.Add(file + " has no " + what);
        return match.Success ? match : null;
    }

    private static List<string> Judge(IReadOnlyDictionary<string, string> t)
    {
        var f = new List<string>();
        var testImage = Pick(t, "tests/compose.yaml", TestImage, "image line with an image default", f);
        var testPort = Pick(t, "tests/compose.yaml", TestPort, "loopback port line with a port default", f);
        var demoImage = Pick(t, "demo/compose.yaml", DemoImage, "image line", f);
        var demoPort = Pick(t, "demo/compose.yaml", DemoPort, "loopback port line", f);
        if (testImage is null || testPort is null || demoImage is null || demoPort is null) return f;
        var ports = new (string, Match)[] { ("tests/compose.yaml", testPort), ("demo/compose.yaml", demoPort) };
        foreach (var (label, port) in ports)
            if (port.Groups["container"].Value != "5432")
                f.Add(label + " maps its host port to container port " + port.Groups["container"].Value + ", not 5432");
        var major = testImage.Groups["major"].Value;
        if (demoImage.Groups["major"].Value != major)
            f.Add("the demo image major " + demoImage.Groups["major"].Value + " differs from the tests image major "
                + major);
        var (test, demo) = (testPort.Groups["host"].Value, demoPort.Groups["host"].Value);
        if (test == demo) f.Add("the tests and the demo publish the same host port " + test);
        Repetitions(t, (major, test, demo), f);
        return f;
    }

    private static void Repetitions(IReadOnlyDictionary<string, string> t, (string Major, string Test, string Demo) v,
        List<string> f)
    {
        var rules = new (Regex Pattern, string Source, string Meaning, string[] Files)[]
        {
            (Major, v.Major, "the PostgreSQL major",
                ["tests/compose.yaml", "demo/compose.yaml", "README.md", "demo/README.md"]),
            (TestUse, v.Test, "the host port of the test server",
                [".github/workflows/ci.yml", "tests/compose.yaml", "README.md"]),
            (DemoUse, v.Demo, "the host port of the demo server",
                [".github/workflows/ci.yml", "demo/compose.yaml", "README.md", "demo/README.md"]),
        };
        foreach (var (pattern, source, meaning, files) in rules)
            foreach (var file in files)
                foreach (var said in pattern.Matches(t[file]).Select(m => Said(m)))
                    if (said != source)
                        f.Add(file + " says " + meaning + " is " + said + ", the compose file says " + source);
    }

    // The text of the first capture group of the match that took part in it (GroupCollection implements several
    // IEnumerable<T> and so takes no LINQ call without a Cast).
    private static string Said(Match m) => m.Groups.Cast<Group>().Skip(1).First(g => g.Success).Value;

    // ---- self-test ----

    private static Dictionary<string, string> Good() => new()
    {
        [".github/workflows/ci.yml"] =
            "      ORMLITE_TEST_POSTGRES: Host=127.0.0.1;Port=55433;Username=postgres\n"
            + "      OrmLite__Port: \"55432\"\n",
        ["tests/compose.yaml"] =
            "    image: ${ORMLITE_TEST_PG_IMAGE:-neemle/pg.lite:19}\n    ports:\n"
            + "      - \"127.0.0.1:${ORMLITE_TEST_PG_PORT:-55433}:5432\"\n",
        ["demo/compose.yaml"] = "    image: neemle/pg.lite:19\n    ports:\n      - \"127.0.0.1:55432:5432\"\n",
        ["README.md"] =
            "the `neemle/pg.lite:19` image, `ORMLITE_TEST_PG_PORT`, default 55433,\n"
            + "'Host=127.0.0.1;Port=55433;Username=postgres'\n"
            + "OrmLite__Port=55432 on 127.0.0.1:55432\n",
        ["demo/README.md"] = "neemle/pg.lite:19 OrmLite__Port=55432 published on 127.0.0.1:55432 only\n",
    };

    private static readonly (string Name, string File, string A, string B, string Expect)[] Cases =
    [
        ("ci test port drifts", ".github/workflows/ci.yml", "Port=55433", "Port=55434", "the test server is 55434"),
        ("ci demo port drifts", ".github/workflows/ci.yml", "\"55432\"", "\"55440\"", "the demo server is 55440"),
        ("tests default port moves", "tests/compose.yaml", "-55433}", "-55440}", "the compose file says 55440"),
        ("tests container port", "tests/compose.yaml", "}:5432", "}:5433", "container port 5433, not 5432"),
        ("demo container port", "demo/compose.yaml", ":5432\"", ":5433\"", "container port 5433, not 5432"),
        ("demo image major", "demo/compose.yaml", "pg.lite:19", "pg.lite:18", "differs from the tests image major 19"),
        ("readme image major", "README.md", "pg.lite:19", "pg.lite:18", "README.md says the PostgreSQL major is 18"),
        ("demo readme image major", "demo/README.md", "pg.lite:19", "pg.lite:20", "demo/README.md says the PostgreSQL"),
        ("readme test port", "README.md", "Port=55433", "Port=5433", "README.md says the host port of the test"),
        ("readme demo port", "README.md", "OrmLite__Port=55432", "OrmLite__Port=5432", "the demo server is 5432"),
        ("demo readme loopback port", "demo/README.md", "1:55432", "1:55499", "the demo server is 55499"),
        ("same host port", "demo/compose.yaml", "1:55432", "1:55433", "publish the same host port 55433"),
        ("no test port variable", "tests/compose.yaml", "ORMLITE_TEST_PG_PORT", "OTHER", "has no loopback port line"),
        ("no demo image", "demo/compose.yaml", "image: neemle", "image: other", "demo/compose.yaml has no image line"),
    ];

    private static bool Holds((string Name, string File, string A, string B, string Expect) c)
    {
        var texts = Good();
        var before = texts[c.File];
        texts[c.File] = before.Replace(c.A, c.B, StringComparison.Ordinal);
        var fails = Judge(texts);
        var ok = texts[c.File] != before && fails.Any(m => m.Contains(c.Expect, StringComparison.Ordinal));
        if (!ok)
            Console.Error.WriteLine("SELF-TEST FAIL: " + c.Name + ": expected `" + c.Expect + "`, got: "
                + (fails.Count == 0 ? "none" : string.Join(" | ", fails)));
        return ok;
    }

    private static int SelfTest()
    {
        var good = Judge(Good());
        if (good.Count > 0) Console.Error.WriteLine("SELF-TEST FAIL: fixture refused: " + string.Join(" | ", good));
        var failed = Cases.Count(c => !Holds(c)) + (good.Count > 0 ? 1 : 0);
        Console.WriteLine("self-test: 1 passing fixture and " + Cases.Length + " mutated cases, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
}
