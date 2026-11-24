#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
// Matrix guard for the jobs `test` (stage 2) and `aot-smoke` (stage 3) of .github/workflows/ci.yml. Both run once per
// .NET runtime, 10.0.x and 11.0.x, because a runtime change (the decimal conversion of a double differs on .NET 11)
// went unnoticed while only .NET 10 ran the tests. A leg that silently ran on the wrong runtime, or a matrix that lost
// a leg, tests nothing. Facts a green build must not lose: the strategy is fail-fast false with exactly the two legs
// (no exclude, no other axis); the job is named by its stage and its version (`2 test (.NET ...)`, `3 aot-smoke
// (.NET ...)`) and runs on ubuntu-latest; the one setup-dotnet step installs the leg's version; a `dotnet --info` step
// comes before the build; every uploaded artifact is named by the leg. The test job sets DOTNET_ROLL_FORWARD (Minor on
// 10, LatestMajor on 11) and ORMLITE_EXPECT_DOTNET at the job env and nowhere else (no step may override either), and
// keeps its upload of TestResults for 7 days; the aot-smoke job passes its target framework to the publish and
// compares the runtime the binary prints. No other job uses a matrix. scripts/StageShape.cs requires the two steps
// that run this script in job static and the needs of both jobs; scripts/WorkflowShape.cs lets job test carry a
// strategy and guards the rest of its shape.
//   dotnet run scripts/MatrixShape.cs -- check [<ci.yml>]    exit 0 pass, 1 shape failure, 2 usage
//   dotnet run scripts/MatrixShape.cs -- self-test           no workflow needed; every assertion has a negative case
// The reader knows the indentation ci.yml uses (job at 2, keys at 4, steps at 6) and fails closed on anything else.
using System.Text.RegularExpressions;
using static System.StringComparison;
using Case = (string Name, string Op, string A, string B, string Expect);
using Spec = (string Job, string Label, string Second, string[] Legs);

if (args.Length == 1 && args[0] == "self-test") return SelfTest();
if (args.Length is 1 or 2 && args[0] == "check") return Check(args.Length == 2 ? args[1] : ".github/workflows/ci.yml");
Console.Error.WriteLine("usage: MatrixShape.cs check [<ci.yml>] | self-test");
return 2;

int Check(string path)
{
    if (!File.Exists(path))
    {
        Console.Error.WriteLine("FAIL: the file " + path + " does not exist");
        return 1;
    }
    var fails = Judge(File.ReadAllText(path));
    foreach (var message in fails) Console.Error.WriteLine("FAIL: " + message);
    if (fails.Count == 0) Console.WriteLine("PASS: the test and aot-smoke jobs keep their .NET 10 and .NET 11 legs.");
    return fails.Count == 0 ? 0 : 1;
}

Spec[] Specs() =>
[
    ("test", "2 test", "roll-forward",
        new[] { "dotnet: 10.0.x roll-forward: Minor", "dotnet: 11.0.x roll-forward: LatestMajor" }),
    ("aot-smoke", "3 aot-smoke", "tfm", new[] { "dotnet: 10.0.x tfm: net10.0", "dotnet: 11.0.x tfm: net11.0" }),
];

List<string> Judge(string text)
{
    var f = new List<string>();
    var lines = Lines(text);
    var outside = lines.ToList();
    foreach (var spec in Specs())
    {
        var header = "  " + spec.Job + ":";
        var job = Below(lines, header);
        if (job.Count == 0)
        {
            f.Add("there is no job `" + spec.Job + "`");
            continue;
        }
        Strategy(spec, job, f);
        Header(spec, job, f);
        StepRules(spec, job, f);
        if (spec.Job == "test") TestRules(lines, job, f);
        else AotRules(job, f);
        outside.RemoveRange(outside.FindIndex(l => l == header), job.Count + 1);
    }
    foreach (var l in outside.Where(l => l.Contains("matrix.") || l.Trim() == "strategy:"))
        f.Add("a matrix is used outside the jobs test and aot-smoke: " + l.Trim());
    return f;
}

// ---- reading: the lines of the file without blanks and comments, and the region below a header ----

int Indent(string line) => line.Length - line.TrimStart(' ').Length;

List<string> Lines(string text) => text.Split('\n').Select(l => l.TrimEnd('\r'))
    .Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith('#')).ToList();

List<string> Below(List<string> lines, string header)
{
    var start = lines.FindIndex(l => l == header);
    var body = new List<string>();
    if (start < 0) return body;
    foreach (var l in lines.Skip(start + 1))
    {
        if (Indent(l) <= Indent(header)) break;
        body.Add(l);
    }
    return body;
}

List<string> Keys(List<string> body, int indent) =>
    body.Where(l => Indent(l) == indent).Select(l => l.Trim()).ToList();

string Join(IEnumerable<string> items) => string.Join(" | ", items);

// each step as its trimmed lines, the "- " of the first one removed
List<List<string>> StepsOf(List<string> job)
{
    var steps = new List<List<string>>();
    foreach (var l in Below(job, "    steps:"))
    {
        var dash = l.StartsWith("      - ", StringComparison.Ordinal);
        if (dash) steps.Add([]);
        if (steps.Count > 0) steps[^1].Add(dash ? l.Trim()[1..].Trim() : l.Trim());
    }
    return steps;
}

string Body(List<string> step) => string.Join("\n", step);

// ---- assertions on every matrix job ----

void Strategy(Spec spec, List<string> job, List<string> f)
{
    var who = "job " + spec.Job;
    if (!job.Contains("    strategy:"))
    {
        f.Add(who + " has no `strategy:`");
        return;
    }
    var body = Below(job, "    strategy:");
    var top = Keys(body, 6);
    if (!top.Order(StringComparer.Ordinal).SequenceEqual(new[] { "fail-fast: false", "matrix:" }))
        f.Add(who + ": the strategy must hold exactly `fail-fast: false` and `matrix:`, it holds " + Join(top));
    var matrix = Below(body, "      matrix:");
    if (!Keys(matrix, 8).SequenceEqual(new[] { "include:" }))
        f.Add(who + ": the matrix must hold only `include:` (no exclude, no other axis)");
    var legs = Legs(Below(matrix, "        include:"));
    if (!legs.Order(StringComparer.Ordinal).SequenceEqual(spec.Legs.Order(StringComparer.Ordinal)))
        f.Add(who + ": the matrix legs must be exactly " + Join(spec.Legs) + ", they are " + Join(legs));
    var allowed = new[] { "dotnet", spec.Second };
    foreach (Match m in Regex.Matches(string.Join("\n", job), @"matrix\.([A-Za-z0-9_-]+)"))
        if (!allowed.Contains(m.Groups[1].Value))
            f.Add(who + " reads matrix." + m.Groups[1].Value + ", which it lacks");
}

List<string> Legs(List<string> include)
{
    var legs = new List<string>();
    foreach (var l in include)
        if (l.TrimStart().StartsWith("- ", StringComparison.Ordinal)) legs.Add(l.Trim()[2..].Trim());
        else if (legs.Count > 0) legs[^1] += " " + l.Trim();
    return legs;
}

void Header(Spec spec, List<string> job, List<string> f)
{
    var name = "    name: " + spec.Label + " (.NET ${{ matrix.dotnet }})";
    if (!job.Contains(name))
        f.Add("job " + spec.Job + " lacks the line `" + name.Trim() + "` (a leg named by its version)");
    if (!job.Contains("    runs-on: ubuntu-latest")) f.Add("job " + spec.Job + " lacks `runs-on: ubuntu-latest`");
}

void StepRules(Spec spec, List<string> job, List<string> f)
{
    var who = "job " + spec.Job;
    var steps = StepsOf(job);
    var setups = steps.Where(s => s.Any(l => l.StartsWith("uses: actions/setup-dotnet@", StringComparison.Ordinal)));
    if (setups.Count() != 1) f.Add(who + " must have exactly one actions/setup-dotnet step, it has " + setups.Count());
    else if (!setups.First().Where(l => l.StartsWith("dotnet-version", StringComparison.Ordinal))
        .SequenceEqual(new[] { "dotnet-version: ${{ matrix.dotnet }}" }))
        f.Add(who + ": setup-dotnet must install exactly `dotnet-version: ${{ matrix.dotnet }}`");
    var info = steps.FindIndex(s => s.Contains("run: dotnet --info"));
    var build = steps.FindIndex(s => Body(s).Contains("dotnet build") || Body(s).Contains("dotnet publish"));
    if (info < 0) f.Add(who + " has no step `run: dotnet --info`, which prints the runtimes of the leg");
    else if (build >= 0 && info > build) f.Add(who + ": the `dotnet --info` step must come before the build");
    var uploads = steps.Where(s => s.Any(l => l.StartsWith("uses: actions/upload-artifact@", Ordinal)));
    foreach (var u in uploads)
        if (!u.Any(l => l.StartsWith("name: ", StringComparison.Ordinal) && l.Contains("${{ matrix.dotnet }}")))
            f.Add(who + ": an upload-artifact step must name its artifact with ${{ matrix.dotnet }}");
}

// ---- assertions on job test ----

void TestRules(List<string> lines, List<string> job, List<string> f)
{
    var env = Below(job, "    env:");
    var rolls = lines.Where(l => l.Contains("ROLL_FORWARD", StringComparison.OrdinalIgnoreCase)
        || l.Contains("ROLLFORWARD", StringComparison.OrdinalIgnoreCase)).ToList();
    var expects = lines.Where(l => l.Contains("ORMLITE_EXPECT_DOTNET", StringComparison.Ordinal)).ToList();
    Once(env, rolls, "DOTNET_ROLL_FORWARD", "${{ matrix.roll-forward }}", f);
    Once(env, expects, "ORMLITE_EXPECT_DOTNET", "${{ matrix.dotnet }}", f);
    foreach (var l in job.Where(l => l.Contains("GITHUB_ENV") || l.Contains("--roll-forward")))
        f.Add("job test must not change its roll-forward policy or its environment: " + l.Trim());
    var kept = StepsOf(job).Any(s => s.Contains("uses: actions/upload-artifact@v4") && s.Contains("if: always()")
        && s.Contains("path: TestResults") && s.Contains("name: test-results-${{ matrix.dotnet }}")
        && s.Contains("retention-days: 7"));
    if (!kept)
        f.Add("job test must keep an always() upload of TestResults named test-results-${{ matrix.dotnet }}, 7 days");
}

// the variable is set at the env of the job, and no other line of the file names it (a step env or an inline
// assignment would override the leg's value)
void Once(List<string> env, List<string> said, string key, string value, List<string> f)
{
    if (!env.Contains("      " + key + ": " + value) || said.Count != 1)
        f.Add(key + " must be set once, in the env of job test, as `" + key + ": " + value + "`");
}

// ---- assertions on job aot-smoke ----

void AotRules(List<string> job, List<string> f)
{
    var publish = StepsOf(job).FirstOrDefault(s => Body(s).Contains("dotnet publish tests/AotSmoke"));
    if (publish is null)
    {
        f.Add("job aot-smoke has no step running `dotnet publish tests/AotSmoke`");
        return;
    }
    var tokens = new[] { "-p:AotSmokeTfm=${{ matrix.tfm }}", "AOT_TFM: ${{ matrix.tfm }}", "grep -qx 'aot smoke ok'",
        "aot runtime: ${AOT_TFM#net}" };
    foreach (var token in tokens.Where(t => !Body(publish).Contains(t, StringComparison.Ordinal)))
        f.Add("the aot-smoke publish step lacks `" + token + "`");
}

// ---- self-test: a passing fixture, and one mutated negative per assertion ----

string Good() => """
name: ci
jobs:
  test:
    name: 2 test (.NET ${{ matrix.dotnet }})
    needs: static
    runs-on: ubuntu-latest
    timeout-minutes: 30
    strategy:
      fail-fast: false
      matrix:
        include:
          - dotnet: 10.0.x
            roll-forward: Minor
          - dotnet: 11.0.x
            roll-forward: LatestMajor
    env:
      DOTNET_ROLL_FORWARD: ${{ matrix.roll-forward }}
      ORMLITE_EXPECT_DOTNET: ${{ matrix.dotnet }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ matrix.dotnet }}
      - name: Print
        run: dotnet --info
      - run: dotnet build X
      - run: dotnet test X
      - name: Keep
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results-${{ matrix.dotnet }}
          path: TestResults
          retention-days: 7
  package:
    needs: test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 10.0.x
  aot-smoke:
    name: 3 aot-smoke (.NET ${{ matrix.dotnet }})
    needs: test
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        include:
          - dotnet: 10.0.x
            tfm: net10.0
          - dotnet: 11.0.x
            tfm: net11.0
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ matrix.dotnet }}
      - run: dotnet --info
      - name: Publish
        env:
          AOT_TFM: ${{ matrix.tfm }}
        run: |
          dotnet publish tests/AotSmoke -c Release -r linux-x64 -p:AotSmokeTfm=${{ matrix.tfm }} -o aot-out
          grep -qx 'aot smoke ok' aot.log
          grep -q "^aot runtime: ${AOT_TFM#net}" aot.log
""";

string Apply(string text, Case c)
{
    var lines = text.Split('\n').ToList();
    var at = lines.FindIndex(l => l.Contains(c.A, StringComparison.Ordinal));
    if (c.Op == "sub") return text.Replace(c.A, c.B, StringComparison.Ordinal);
    if (at < 0) return text;
    if (c.Op == "drop") return string.Join('\n', lines.Where(l => !l.Contains(c.A, StringComparison.Ordinal)));
    if (c.Op == "ins") lines.Insert(at + 1, c.B);
    if (c.Op != "before") return string.Join('\n', lines);
    var moved = lines[at];
    lines.RemoveAt(at);
    lines.Insert(lines.FindIndex(l => l.Contains(c.B, StringComparison.Ordinal)), moved);
    return string.Join('\n', lines);
}

// an operation ending in @aot applies to the aot-smoke job only (the last job of the fixture)
string Mutate(Case c)
{
    var scoped = c.Op.EndsWith("@aot", StringComparison.Ordinal);
    var cut = scoped ? Good().IndexOf("  aot-smoke:", StringComparison.Ordinal) : 0;
    return Good()[..cut] + Apply(Good()[cut..], scoped ? (c.Name, c.Op[..^4], c.A, c.B, c.Expect) : c);
}

bool Holds(Case c)
{
    var mutated = Mutate(c);
    var fails = Judge(mutated);
    var ok = mutated != Good() && fails.Any(m => m.Contains(c.Expect, StringComparison.Ordinal));
    if (!ok)
        Console.Error.WriteLine($"SELF-TEST FAIL: {c.Name}: expected `{c.Expect}`, applied: {mutated != Good()}, got: "
            + (fails.Count == 0 ? "none" : Join(fails)));
    return ok;
}

int SelfTest()
{
    var good = Judge(Good());
    if (good.Count > 0) Console.Error.WriteLine("SELF-TEST FAIL: the fixture is refused: " + Join(good));
    var cases = Cases();
    var failed = cases.Count(c => !Holds(c)) + (good.Count > 0 ? 1 : 0);
    Console.WriteLine("self-test: 1 passing fixture and " + cases.Count + " mutated cases, " + failed + " failed");
    return failed == 0 ? 0 : 1;
}

// a case is name ~ operation ~ operand a ~ operand b ~ expected failure. The operations: sub (replace a by b, all
// occurrences), drop (remove the lines with a), ins (add the line b after the first line with a) and before (move the
// first line with a before the first line with b); \n in an operand is a line break.
List<Case> Cases() =>
    (StrategyCases() + "\n" + RuleCases()).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('~'))
        .Select(p => (p[0].Trim(), p[1], p[2].Replace("\\n", "\n"), p[3].Replace("\\n", "\n"), p[4])).ToList();

string StrategyCases() => """
    fail-fast on~sub~fail-fast: false~fail-fast: true~must hold exactly `fail-fast: false`
    no fail-fast~drop~fail-fast: false~~must hold exactly `fail-fast: false`
    max-parallel added~ins~fail-fast: false~      max-parallel: 1~must hold exactly `fail-fast: false`
    no strategy~sub~strategy:~xstrategy:~job test has no `strategy:`
    no aot strategy~sub@aot~strategy:~xstrategy:~job aot-smoke has no `strategy:`
    an exclude~ins~      matrix:~        exclude:~must hold only `include:`
    another axis~ins~      matrix:~        os: [a]~must hold only `include:`
    the 11 leg gone~sub~          - dotnet: 11.0.x\n            roll-forward: LatestMajor\n~~legs must be exactly
    the 10 leg gone~sub~          - dotnet: 10.0.x\n            roll-forward: Minor\n~~legs must be exactly
    a third leg~ins~roll-forward: LatestMajor~          - dotnet: 9.0.x\n            roll-forward: Minor~legs must be
    the 10 leg rolls forward~sub~roll-forward: Minor~roll-forward: LatestMajor~legs must be exactly
    the 11 leg does not roll forward~sub~roll-forward: LatestMajor~roll-forward: Minor~legs must be exactly
    the 11 aot leg targets net10~sub~tfm: net11.0~tfm: net10.0~legs must be exactly
    aot fail-fast on~sub@aot~fail-fast: false~fail-fast: true~job aot-smoke: the strategy
    no test job~sub~  test:~  tests:~there is no job `test`
    no aot job~sub~  aot-smoke:~  aot:~there is no job `aot-smoke`
    """;

string RuleCases() => """
    unnamed leg~sub~name: 2 test (.NET ${{ matrix.dotnet }})~name: test~job test lacks the line `name: 2 test
    unnamed aot leg~sub~name: 3 aot-smoke (.NET ${{ matrix.dotnet }})~name: aot-smoke~job aot-smoke lacks the line
    a leg in the wrong stage~sub~name: 2 test (~name: 3 test (~job test lacks the line `name: 2 test
    an aot leg in the wrong stage~sub~name: 3 aot-smoke (~name: 2 aot-smoke (~job aot-smoke lacks the line
    runner by expression~sub~runs-on: ubuntu-latest\n    timeout~runs-on: x\n    timeout~lacks `runs-on: ubuntu-latest`
    a fixed version~sub~dotnet-version: ${{ matrix.dotnet }}~dotnet-version: 10.0.x~must install exactly
    a second version~ins~dotnet-version: ${{ matrix.dotnet }}~          dotnet-version: 9.0.x~must install exactly
    no setup-dotnet~sub~uses: actions/setup-dotnet@v4~uses: actions/cache@v4~exactly one actions/setup-dotnet step
    no info step~sub~run: dotnet --info~run: dotnet --version~has no step `run: dotnet --info`
    info after the build~before~run: dotnet build X~name: Print~step must come before the build
    an upload without the leg~sub~name: test-results-${{ matrix.dotnet }}~name: test-results~must name its artifact
    no upload~sub~uses: actions/upload-artifact@v4~uses: actions/cache@v4~must keep an always() upload
    no retention days~drop~retention-days: 7~~must keep an always() upload
    a longer retention~sub~retention-days: 7~retention-days: 90~must keep an always() upload
    an upload that is not always~sub~if: always()~if: success()~must keep an always() upload
    an upload of another path~sub~path: TestResults~path: out~must keep an always() upload
    no roll-forward variable~drop~DOTNET_ROLL_FORWARD: ${{~~DOTNET_ROLL_FORWARD must be set once
    a fixed roll-forward~sub~FORWARD: ${{ matrix.roll-forward }}~FORWARD: Minor~ROLL_FORWARD must be
    a step overrides it~ins~run: dotnet test X~        env:\n          DOTNET_ROLL_FORWARD: A~ROLL_FORWARD must be
    an inline roll-forward~sub~run: dotnet test X~run: DOTNET_ROLL_FORWARD=Minor dotnet test X~ROLL_FORWARD must be
    the alias spelling~ins~run: dotnet test X~        env:\n          DOTNET_ROLLFORWARD: Minor~ROLL_FORWARD must be
    the command line policy~sub~run: dotnet test X~run: dotnet test X --roll-forward Minor~must not change its roll
    the environment file~sub~run: dotnet --info~run: echo A=b >> $GITHUB_ENV~must not change its roll
    no expected runtime~drop~ORMLITE_EXPECT_DOTNET: ${{~~ORMLITE_EXPECT_DOTNET must be set once
    a fixed expected runtime~sub~ORMLITE_EXPECT_DOTNET: ${{ matrix.dotnet }}~ORMLITE_EXPECT_DOTNET: 10.0.x~must be set
    the expected runtime on a step~ins~run: dotnet test X~        env:\n          ORMLITE_EXPECT_DOTNET: 10~must be set
    an unknown matrix key~sub@aot~- run: dotnet --info~- run: echo ${{ matrix.os }}~reads matrix.os
    the matrix on another job~sub~dotnet-version: 10.0.x~dotnet-version: ${{ matrix.dotnet }}~outside the jobs test and
    a strategy on another job~ins~    needs: test~    strategy:~outside the jobs test and
    no aot publish~sub~dotnet publish tests/AotSmoke~dotnet build tests/AotSmoke~no step running `dotnet publish
    no target framework passed~sub~ -p:AotSmokeTfm=${{ matrix.tfm }}~~lacks `-p:AotSmokeTfm=
    no expected runtime env on aot~drop~AOT_TFM: ${{~~lacks `AOT_TFM: ${{ matrix
    no aot runtime comparison~drop~aot runtime: ~~lacks `aot runtime: ${AOT
    no aot ok line~sub~grep -qx 'aot smoke ok' aot.log~echo~lacks `grep -qx 'aot smoke ok'`
    """;
