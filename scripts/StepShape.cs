#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
// Step guard for job `test` of .github/workflows/ci.yml (ORML-52, ORML-56, ORML-78, ORML-79). Facts a green build
// must not lose: the main test step is bounded by a step timeout below the job's, with a console log that names the
// last test; every `dotnet test` step caps xUnit at one thread (the parallel run was killed for memory); the
// data-literal tests run once more under a non-UTC TZ with a TRX and a gate of their own (the default UTC runner
// cannot tell the Local to UTC conversion of a moment from none). scripts/StageShape.cs requires the two steps that
// run this script in job static (stage 1); scripts/WorkflowShape.cs guards the rest of the shape of job test. The
// matrix itself (strategy, name, env) sits beside the steps and is read past, so each leg is judged as one job test.
//   dotnet run scripts/StepShape.cs -- check [<ci.yml>]    exit 0 pass, 1 shape failure, 2 usage
//   dotnet run scripts/StepShape.cs -- self-test           no workflow needed; every assertion has a negative case
// The reader knows the indentation ci.yml uses (job at 2, steps at 6, keys at 8) and fails closed on anything else.
using Case = (string Name, string Op, string A, string B, string Expect);
using Step = (
    System.Collections.Generic.Dictionary<string, string> Keys,
    System.Collections.Generic.Dictionary<string, string> Env);

if (args.Length == 1 && args[0] == "self-test") return SelfTest();
if (args.Length is 1 or 2 && args[0] == "check") return Check(args.Length == 2 ? args[1] : ".github/workflows/ci.yml");
Console.Error.WriteLine("usage: StepShape.cs check [<ci.yml>] | self-test");
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
    if (fails.Count == 0) Console.WriteLine("PASS: the thread cap, the zone step and its gate stand.");
    return fails.Count == 0 ? 0 : 1;
}

List<string> Judge(string text)
{
    var f = new List<string>();
    var lines = text.Split('\n').Select(l => l.TrimEnd('\r'))
        .Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith('#')).ToList();
    var steps = ReadSteps(lines, f);
    var runs = steps.Select(s => s.Keys.GetValueOrDefault("run") ?? "").ToList();
    if (!runs.Any(r => Has(r, "dotnet test"))) f.Add("job test has no step running `dotnet test`");
    foreach (var i in Enumerable.Range(0, runs.Count).Where(i => Has(runs[i], "dotnet test")))
        if (!Has(runs[i], "-- xUnit.MaxParallelThreads=1"))
            f.Add($"step {i + 1} runs dotnet test and lacks `-- xUnit.MaxParallelThreads=1`");
    var zones = Enumerable.Range(0, steps.Count).Where(i => steps[i].Env.ContainsKey("TZ")).ToList();
    if (lines.Count(l => l.TrimStart().StartsWith("TZ:", StringComparison.Ordinal)) != zones.Count)
        f.Add("TZ is set outside the env of a step of job test");
    if (zones.Count != 1) f.Add($"job test must have exactly one step with env TZ, it has {zones.Count}");
    else Zone(steps, runs, zones[0], f);
    foreach (var i in Enumerable.Range(0, runs.Count).Where(i => Has(runs[i], "dotnet test") && !zones.Contains(i)))
        MainStep(lines, steps[i], (i + 1, runs[i]), f);
    return f;
}

// ---- reading: the steps of job `test`, each as its keys (a block scalar or a mapping joined) and its env ----

bool Has(string text, string token) => text.Contains(token, StringComparison.Ordinal);

int Indent(string line) => line.Length - line.TrimStart(' ').Length;

List<string> Region(List<string> lines, string header, int indent)
{
    var start = lines.FindIndex(l => l == header);
    var body = new List<string>();
    if (start < 0) return body;
    foreach (var l in lines.Skip(start + 1))
    {
        if (Indent(l) <= indent) break;
        body.Add(l);
    }
    return body;
}

List<Step> ReadSteps(List<string> lines, List<string> f)
{
    var steps = new List<Step>();
    string? open = null;
    foreach (var raw in Region(Region(lines, "  test:", 2), "    steps:", 4))
    {
        var dash = raw.StartsWith("      - ", StringComparison.Ordinal);
        if (dash) steps.Add((new Dictionary<string, string>(), new Dictionary<string, string>()));
        var line = dash ? "        " + raw[8..] : raw;
        if (steps.Count == 0) f.Add("cannot be read as a step: " + raw.Trim());
        else if (Indent(line) == 8) open = ReadKey(steps[^1], line.Trim(), f);
        else if (Indent(line) > 8 && open is not null) Child(steps[^1], open, line.Trim());
        else f.Add("cannot be read: " + raw.Trim());
    }
    if (steps.Count == 0) f.Add("job test has no steps at `  test:` / `    steps:` / `      - `");
    return steps;
}

string? ReadKey(Step step, string text, List<string> f)
{
    var colon = text.IndexOf(':');
    if (colon <= 0)
    {
        f.Add("cannot be read as `key: value`: " + text);
        return null;
    }
    var value = text[(colon + 1)..].Trim();
    var opens = value.Length == 0 || value[0] is '>' or '|';
    step.Keys[text[..colon]] = opens ? "" : value;
    return opens ? text[..colon] : null;
}

void Child(Step step, string key, string text)
{
    var colon = text.IndexOf(':');
    if (key == "env" && colon > 0) step.Env[text[..colon]] = text[(colon + 1)..].Trim();
    else step.Keys[key] = (step.Keys.GetValueOrDefault(key, "") + " " + text).Trim();
}

// ---- assertions on the zone step and its gate ----

void Zone(List<Step> steps, List<string> runs, int zone, List<string> f)
{
    var name = steps[zone].Env["TZ"];
    if (new[] { "", "UTC", "Etc/UTC", "GMT" }.Contains(name)) f.Add($"the zone step sets TZ to `{name}`, which is UTC");
    var gate = runs.FindIndex(zone + 1, r => Has(r, "check TestResults/zone.trx"));
    var main = runs.FindIndex(r => Has(r, "check TestResults/tests.trx"));
    if (gate < 0) f.Add("no step after the zone step runs `check TestResults/zone.trx`");
    if (zone < main) f.Add("the zone step must come after the skip gate step of the main run");
    Need(runs[zone], "the zone step", ["dotnet test", "--no-build", "--filter", "LogFileName=zone.trx"], f);
    if (gate >= 0)
        Need(runs[gate], "the zone gate step",
            ["tests/zone-allowlist.txt", "--require-postgres tests/zone-witness.txt"], f);
    foreach (var s in new[] { zone, gate }.Where(s => s >= 0 && steps[s].Keys.ContainsKey("if")))
        f.Add($"step {s + 1}: a zone step has an `if:`");
}

void Need(string run, string who, string[] tokens, List<string> f)
{
    foreach (var token in tokens.Where(t => !Has(run, t))) f.Add(who + " lacks `" + token + "`");
}

// ---- assertions on the main test step: a stall fails the step, and the log names the last test (ORML-78) ----

int Minutes(string? value) => int.TryParse(value, out var minutes) ? minutes : -1;

void MainStep(List<string> lines, Step step, (int Number, string Run) at, List<string> f)
{
    var jobLine = Region(lines, "  test:", 2)
        .FirstOrDefault(l => l.StartsWith("    timeout-minutes:", StringComparison.Ordinal));
    var job = Minutes(jobLine?.Split(':')[1].Trim());
    var own = Minutes(step.Keys.GetValueOrDefault("timeout-minutes"));
    if (own is < 1 or > 15)
        f.Add($"step {at.Number} (the main test step) needs a numeric `timeout-minutes` of 1 to 15, it has `{own}`");
    else if (job <= own)
        f.Add($"step {at.Number}: its timeout-minutes {own} must be below the timeout-minutes of job test ({job})");
    if (!Has(at.Run, "--logger \"console;verbosity=detailed\""))
        f.Add($"step {at.Number} (the main test step) lacks `--logger \"console;verbosity=detailed\"`");
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
    env:
      DOTNET_ROLL_FORWARD: ${{ matrix.roll-forward }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ matrix.dotnet }}
      - run: dotnet build X
      - name: Test
        timeout-minutes: 15
        env:
          ORMLITE_TEST_POSTGRES: Host=127.0.0.1;Port=55433;Username=postgres
        run: >-
          dotnet test X --no-build --logger "trx;LogFileName=tests.trx" --logger "console;verbosity=detailed"
          -- xUnit.MaxParallelThreads=1
      - run: dotnet run scripts/SkipGate.cs -- check TestResults/tests.trx tests/skip-allowlist.txt --require-postgres w
      - name: Zone
        env:
          TZ: Pacific/Auckland
        run: >-
          dotnet test X --no-build --filter DataLiteral --logger "trx;LogFileName=zone.trx"
          -- xUnit.MaxParallelThreads=1
      - run: >-
          dotnet run scripts/SkipGate.cs -- check TestResults/zone.trx tests/zone-allowlist.txt
          --require-postgres tests/zone-witness.txt
      - name: Keep
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: test-results-${{ matrix.dotnet }}
          path: TestResults
          retention-days: 7
  package:
    runs-on: ubuntu-latest
    steps:
      - run: echo done
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

bool Holds(Case c)
{
    var mutated = Apply(Good(), c);
    var fails = Judge(mutated);
    var ok = mutated != Good() && fails.Any(m => m.Contains(c.Expect, StringComparison.Ordinal));
    if (!ok)
        Console.Error.WriteLine($"SELF-TEST FAIL: {c.Name}: expected `{c.Expect}`, applied: {mutated != Good()}, got: "
            + (fails.Count == 0 ? "none" : string.Join(" | ", fails)));
    return ok;
}

int SelfTest()
{
    var good = Judge(Good());
    if (good.Count > 0) Console.Error.WriteLine("SELF-TEST FAIL: the fixture is refused: " + string.Join(" | ", good));
    var cases = CaseTable();
    var failed = cases.Count(c => !Holds(c)) + (good.Count > 0 ? 1 : 0);
    Console.WriteLine("self-test: 1 passing fixture and " + cases.Count + " mutated cases, " + failed + " failed");
    return failed == 0 ? 0 : 1;
}

// a case is name ~ operation ~ operand a ~ operand b ~ expected failure. The operations: sub (replace a by b), drop
// (remove the lines with a), ins (add the line b after the line with a) and before (move the line with a before the
// line with b).
List<Case> CaseTable() =>
    """
    no thread cap~sub~ -- xUnit.MaxParallelThreads=1~~lacks `-- xUnit.MaxParallelThreads=1`
    a thread cap of two~sub~MaxParallelThreads=1~MaxParallelThreads=2~lacks `-- xUnit.MaxParallelThreads=1`
    no test step~sub~dotnet test~dotnet echo~has no step running `dotnet test`
    no zone step~sub~TZ: Pacific/Auckland~ZONE: Pacific/Auckland~exactly one step with env TZ
    zone set to UTC~sub~TZ: Pacific/Auckland~TZ: UTC~sets TZ to `UTC`
    zone on the main step too~ins~ORMLITE_TEST_POSTGRES:~          TZ: Pacific/Auckland~exactly one step with env TZ
    zone at job level~ins~    runs-on: ubuntu-latest~    env:\n      TZ: Pacific/Auckland~TZ is set outside the env
    no zone gate~drop~check TestResults/zone.trx~~no step after the zone step runs
    zone step before the skip gate~before~--require-postgres w~- run: >-~after the skip gate step
    zone step without a filter~sub~ --filter DataLiteral~~lacks `--filter`
    zone step on the main TRX~sub~LogFileName=zone.trx~LogFileName=tests.trx~lacks `LogFileName=zone.trx`
    zone gate without the allow-list~sub~tests/zone-allowlist.txt~x.txt~lacks `tests/zone-allowlist.txt`
    zone gate without the witnesses~sub~ --require-postgres tests/zone-witness.txt~~lacks `--require-postgres tests/z
    an if on the zone step~ins~TZ: Pacific/Auckland~        if: always()~a zone step has an `if:`
    no step timeout~drop~timeout-minutes: 15~~needs a numeric `timeout-minutes` of 1 to 15
    a step timeout of sixteen~sub~timeout-minutes: 15~timeout-minutes: 16~needs a numeric `timeout-minutes` of 1 to 15
    a step timeout that is no number~sub~timeout-minutes: 15~timeout-minutes: soon~needs a numeric `timeout-minutes`
    a job timeout below the step timeout~sub~timeout-minutes: 30~timeout-minutes: 10~must be below the timeout-minutes
    no job timeout~drop~timeout-minutes: 30~~must be below the timeout-minutes of job test (-1)
    no console logger~sub~ --logger "console;verbosity=detailed"~~lacks `--logger "console;verbosity=detailed"`
    """.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('~'))
        .Select(p => (p[0].Trim(), p[1], p[2].Replace("\\n", "\n"), p[3].Replace("\\n", "\n"), p[4])).ToList();
