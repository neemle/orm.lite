#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
// Stage guard for .github/workflows/ci.yml. The pipeline is four ordered stages, so that a broken gate or build stops
// the run before the expensive work: 1 static (every guard and self-test that needs no server, then a strict build),
// 2 test (the .NET 10 and .NET 11 legs), 3 package (pack, the consumer matrix and the guards that need the packages,
// and the NativeAOT smoke legs) and 4 demo (the demo on SQLite and on PostgreSQL). Facts a green build must not lose:
// the file holds exactly the jobs of those stages and no other; each job is named by its stage (`1 static`,
// `2 test (.NET ...)`, ...), which is how the run view reads; each job has exactly the `needs:` line of its stage (none
// for stage 1, so a job with no needs or one moved to another stage is refused); each job still runs the steps its
// stage owns, in order and with no `if:` (a gate that can be skipped gates nothing) ; no job has a job-level `if:`
// (it could skip a whole stage); `continue-on-error` is nowhere in the file; every upload-artifact step names a
// `retention-days:` of 1 to 14. The matrix legs and the roll-forward variables are scripts/MatrixShape.cs, the steps
// of the test job are scripts/WorkflowShape.cs and scripts/StepShape.cs.
//   dotnet run scripts/StageShape.cs -- check [<ci.yml>]    exit 0 pass, 1 shape failure, 2 usage
//   dotnet run scripts/StageShape.cs -- self-test           no workflow needed; every assertion has a negative case
// The reader knows the indentation ci.yml uses (job at 2, keys at 4, steps at 6, step keys at 8) and fails closed on
// anything else: a step is matched by the commands of its `run:` value (a `|` block is one command per line, a folded
// `>` block is one command), a command is the token or starts with the token and a space.
using Case = (string Name, string Op, string A, string B, string Expect);
using Stage = (string Job, string Name, string Needs, string[] Tokens);

if (args.Length == 1 && args[0] == "self-test") return SelfTest();
if (args.Length is 1 or 2 && args[0] == "check") return Check(args.Length == 2 ? args[1] : ".github/workflows/ci.yml");
Console.Error.WriteLine("usage: StageShape.cs check [<ci.yml>] | self-test");
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
    if (fails.Count == 0) Console.WriteLine("PASS: the jobs keep their stages, their needs and their steps.");
    return fails.Count == 0 ? 0 : 1;
}

// the stages in order; a Needs of "" means the job has no needs line; the tokens are in the order of the job
Stage[] Stages() =>
[
    ("static", "1 static", "", StaticTokens()),
    ("test", "2 test (.NET ${{ matrix.dotnet }})", "static",
        new[] { "dotnet build", "dotnet test", "dotnet run scripts/SkipGate.cs -- check" }),
    ("package", "3 package", "test", PackageTokens()),
    ("aot-smoke", "3 aot-smoke (.NET ${{ matrix.dotnet }})", "test", new[] { "dotnet publish tests/AotSmoke" }),
    ("demo-sqlite", "4 demo-sqlite", "package", new[] { "grep -qx 'demo ok'" }),
    ("demo-postgres", "4 demo-postgres", "package",
        new[] { "docker compose -f demo/compose.yaml up -d --wait", "grep -qx 'demo ok'" }),
];

string[] StaticTokens() =>
[
    "dotnet run scripts/SkipGate.cs -- self-test",
    "dotnet run scripts/WorkflowShape.cs -- self-test", "dotnet run scripts/WorkflowShape.cs -- check",
    "dotnet run scripts/StepShape.cs -- self-test", "dotnet run scripts/StepShape.cs -- check",
    "dotnet run scripts/MatrixShape.cs -- self-test", "dotnet run scripts/MatrixShape.cs -- check",
    "dotnet run scripts/ComposeCoherence.cs -- self-test", "dotnet run scripts/ComposeCoherence.cs -- check",
    "dotnet run scripts/StageShape.cs -- self-test", "dotnet run scripts/StageShape.cs -- check",
    "dotnet run scripts/RetentionShape.cs -- self-test", "dotnet run scripts/RetentionShape.cs -- check",
    "dotnet run scripts/RunRetention.cs -- self-test",
    "dotnet build Neemle.Orm.Lite.slnx -c Release -warnaserror",
];

string[] PackageTokens() =>
[
    "dotnet pack Neemle.Orm.Lite.slnx",
    "dotnet run scripts/ReadmeCheck.cs -- self-test", "dotnet run scripts/ReadmeCheck.cs -- source",
    "dotnet run scripts/PackVersions.cs -- self-test", "dotnet run scripts/PackCheck.cs -- package artifacts",
    "dotnet run scripts/PackCheck.cs -- demo-guard-self-test", "dotnet run scripts/PackCheck.cs -- demo-guard",
];

List<string> Judge(string text)
{
    var f = new List<string>();
    var lines = Lines(text);
    var stages = Stages();
    var ids = Below(lines, "jobs:").Where(l => Indent(l) == 2).Select(l => l.Trim().TrimEnd(':')).ToList();
    foreach (var id in ids.Where(id => !stages.Any(s => s.Job == id)))
        f.Add("job `" + id + "` belongs to no stage (the stages are 1 static, 2 test, 3 package, 4 demo)");
    foreach (var stage in stages) JudgeStage(lines, stage, f);
    foreach (var l in lines.Where(l => l.Contains("continue-on-error", StringComparison.Ordinal)))
        f.Add("continue-on-error is not allowed: " + l.Trim());
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

// each step as its lines, the "- " of the first one replaced by two spaces so that every step key is at column 8
List<List<string>> StepsOf(List<string> job)
{
    var steps = new List<List<string>>();
    foreach (var l in Below(job, "    steps:"))
    {
        var dash = l.StartsWith("      - ", StringComparison.Ordinal);
        if (dash) steps.Add([]);
        if (steps.Count > 0) steps[^1].Add(dash ? "        " + l[8..] : l);
    }
    return steps;
}

List<string> Commands(List<string> step)
{
    var at = step.FindIndex(l => l.StartsWith("        run:", StringComparison.Ordinal));
    if (at < 0) return [];
    var value = step[at].Trim()[4..].Trim();
    var body = step.Skip(at + 1).TakeWhile(l => Indent(l) > 8).Select(l => l.Trim()).ToList();
    if (value.Length == 0 || value[0] == '|') return body;
    if (value[0] == '>') return [string.Join(" ", body)];
    return [value];
}

int Find(List<List<string>> steps, string token) => steps.FindIndex(s => Commands(s)
    .Any(c => c == token || c.StartsWith(token + " ", StringComparison.Ordinal)));

// ---- assertions on one job ----

void JudgeStage(List<string> lines, Stage stage, List<string> f)
{
    var job = Below(lines, "  " + stage.Job + ":");
    if (job.Count == 0)
    {
        f.Add("there is no job `" + stage.Job + "`");
        return;
    }
    var keys = job.Where(l => Indent(l) == 4).Select(l => l.Trim()).ToList();
    if (!keys.Contains("name: " + stage.Name))
        f.Add("job " + stage.Job + " lacks the line `name: " + stage.Name + "` (its stage, as the run view shows it)");
    var needs = keys.Where(k => k.StartsWith("needs:", StringComparison.Ordinal)).ToList();
    var want = stage.Needs.Length == 0 ? "no needs line" : "`needs: " + stage.Needs + "`";
    if (!needs.SequenceEqual(stage.Needs.Length == 0 ? new string[0] : new[] { "needs: " + stage.Needs }))
        f.Add("job " + stage.Job + " must have exactly the needs line " + want + ", it has: " + Join(needs));
    if (keys.Any(k => k.StartsWith("if:", StringComparison.Ordinal)))
        f.Add("job " + stage.Job + " has a job-level `if:`, which could skip its whole stage");
    Required(StepsOf(job), stage, f);
}

void Required(List<List<string>> steps, Stage stage, List<string> f)
{
    var at = stage.Tokens.Select(t => Find(steps, t)).ToArray();
    for (var k = 0; k < at.Length; k++)
        if (at[k] < 0) f.Add("job " + stage.Job + " has no step running `" + stage.Tokens[k] + "`");
        else if (k > 0 && at[k - 1] >= at[k])
            f.Add($"job {stage.Job}: the step `{stage.Tokens[k]}` must come after `{stage.Tokens[k - 1]}`");
    foreach (var s in at.Where(s => s >= 0))
        if (steps[s].FirstOrDefault(l => l.StartsWith("        if:", StringComparison.Ordinal)) is { } c
            && c.Trim() != "if: success()")
            f.Add("job " + stage.Job + ": a step has `" + c.Trim() + "`, a gate that can be skipped");
    Uploads(steps, stage.Job, f);
}

void Uploads(List<List<string>> steps, string job, List<string> f)
{
    foreach (var s in steps.Where(s => s.Any(l => l.Contains("uses: actions/upload-artifact@"))))
    {
        var days = s.Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("retention-days: "));
        if (days is null || !int.TryParse(days[16..], out var n) || n is < 1 or > 14)
            f.Add("job " + job + ": an upload-artifact step needs a `retention-days:` of 1 to 14");
    }
}

// ---- self-test: a passing fixture, and one mutated negative per assertion ----

string Good() => """
name: ci
jobs:
  static:
    name: 1 static
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v7
      - run: dotnet run scripts/SkipGate.cs -- self-test
      - run: dotnet run scripts/WorkflowShape.cs -- self-test
      - run: dotnet run scripts/WorkflowShape.cs -- check
      - run: dotnet run scripts/StepShape.cs -- self-test
      - run: dotnet run scripts/StepShape.cs -- check
      - run: dotnet run scripts/MatrixShape.cs -- self-test
      - run: dotnet run scripts/MatrixShape.cs -- check
      - run: dotnet run scripts/ComposeCoherence.cs -- self-test
      - run: dotnet run scripts/ComposeCoherence.cs -- check
      - run: dotnet run scripts/StageShape.cs -- self-test
      - run: dotnet run scripts/StageShape.cs -- check
      - run: dotnet run scripts/RetentionShape.cs -- self-test
      - run: dotnet run scripts/RetentionShape.cs -- check
      - run: dotnet run scripts/RunRetention.cs -- self-test
      - run: dotnet build Neemle.Orm.Lite.slnx -c Release -warnaserror
  test:
    name: 2 test (.NET ${{ matrix.dotnet }})
    needs: static
    runs-on: ubuntu-latest
    steps:
      - run: dotnet build Neemle.Orm.Lite.slnx -c Release
      - name: Test
        run: >-
          dotnet test X --no-build
          -- xUnit.MaxParallelThreads=1
      - run: dotnet run scripts/SkipGate.cs -- check TestResults/tests.trx
      - name: Keep
        if: always()
        uses: actions/upload-artifact@v7
        with:
          name: test-results-${{ matrix.dotnet }}
          path: TestResults
          retention-days: 7
  package:
    name: 3 package
    needs: test
    runs-on: ubuntu-latest
    steps:
      - run: dotnet pack Neemle.Orm.Lite.slnx -c Release -o artifacts -m:1
      - run: dotnet run scripts/ReadmeCheck.cs -- self-test
      - run: dotnet run scripts/ReadmeCheck.cs -- source
      - run: dotnet run scripts/PackVersions.cs -- self-test
      - run: dotnet run scripts/PackCheck.cs -- package artifacts
      - run: dotnet run scripts/PackCheck.cs -- demo-guard-self-test
      - run: dotnet run scripts/PackCheck.cs -- demo-guard
  aot-smoke:
    name: 3 aot-smoke (.NET ${{ matrix.dotnet }})
    needs: test
    runs-on: ubuntu-latest
    steps:
      - run: dotnet publish tests/AotSmoke -c Release
  demo-sqlite:
    name: 4 demo-sqlite
    needs: package
    runs-on: ubuntu-latest
    steps:
      - run: |
          dotnet run --project demo -c Release | tee demo.log
          grep -qx 'demo ok' demo.log
  demo-postgres:
    name: 4 demo-postgres
    needs: package
    runs-on: ubuntu-latest
    steps:
      - run: docker compose -f demo/compose.yaml up -d --wait --wait-timeout 120
      - run: |
          dotnet run --project demo -c Release --no-build | tee demo.log
          grep -qx 'demo ok' demo.log
""";

string Join(IEnumerable<string> items) => string.Join(" | ", items);

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
    var cases = Cases();
    var failed = cases.Count(c => !Holds(c)) + (good.Count > 0 ? 1 : 0);
    Console.WriteLine("self-test: 1 passing fixture and " + cases.Count + " mutated cases, " + failed + " failed");
    return failed == 0 ? 0 : 1;
}

// a case is name ~ operation ~ operand a ~ operand b ~ expected failure. The operations: sub (replace a by b, all
// occurrences), drop (remove the lines with a), ins (add the line b after the first line with a) and before (move the
// first line with a before the first line with b); \n in an operand is a line break. Besides the table, every step
// that a stage requires is removed once, so a gate step that nobody wrote a case for is still pinned.
List<Case> Cases()
{
    var table = CaseTable().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('~'))
        .Select(p => (p[0].Trim(), p[1], p[2].Replace("\\n", "\n"), p[3].Replace("\\n", "\n"), p[4])).ToList();
    var removed = Stages().SelectMany(s => s.Tokens).Distinct()
        .Select(t => ("a removed step: " + t, "drop", t, "", "no step running `" + t + "`"));
    return table.Concat(removed).ToList();
}

string CaseTable() => """
    test without needs~drop~    needs: static~~job test must have exactly the needs line `needs: static`
    test needs another stage~sub~    needs: static~    needs: package~job test must have exactly the needs line
    package without needs~drop~    needs: test~~job package must have exactly the needs line
    package needs the static stage~sub~    needs: test~    needs: static~job package must have exactly
    demos moved up to the test stage~sub~    needs: package~    needs: test~job demo-sqlite must have exactly
    static with a needs line~ins~  static:~    needs: test~job static must have exactly the needs line no needs
    a list of needs~sub~    needs: static~    needs:\n      - static~job test must have exactly
    a job moved to the wrong stage~sub~name: 3 package~name: 2 package~job package lacks the line `name: 3 package`
    a leg named without its stage~sub~name: 2 test (.NET~name: test (.NET~job test lacks the line `name: 2 test
    an aot leg named as the test stage~sub~name: 3 aot-smoke~name: 2 aot-smoke~job aot-smoke lacks the line
    the static stage renamed~sub~name: 1 static~name: static~job static lacks the line `name: 1 static`
    an extra job~sub~  demo-sqlite:~  demo-extra:~belongs to no stage
    no demo job~sub~  demo-sqlite:~  demo-extra:~there is no job `demo-sqlite`
    no aot job~sub~  aot-smoke:~  aot:~there is no job `aot-smoke`
    no static job~sub~  static:~  fast:~there is no job `static`
    a job with an if~ins~  static:~    if: always()~job static has a job-level `if:`
    a step with an if~ins~SkipGate.cs -- self-test~        if: always()~a gate that can be skipped
    an if on the test gate~ins~SkipGate.cs -- check~        if: false~a gate that can be skipped
    continue-on-error on a job~ins~  static:~    continue-on-error: true~continue-on-error is not allowed
    continue-on-error on a step~ins~StageShape.cs -- check~        continue-on-error: true~continue-on-error is not
    continue-on-error on a demo~ins~  demo-postgres:~    continue-on-error: true~continue-on-error is not allowed
    the build before the guards~before~-warnaserror~SkipGate.cs -- self-test~must come after
    a check before its self-test~before~StepShape.cs -- check~StepShape.cs -- self-test~must come after
    the retention check first~before~RetentionShape.cs -- check~RetentionShape.cs -- self-test~must come after
    a build that is not strict~sub~ -c Release -warnaserror~ -c Release~Release -warnaserror`
    the ReadmeCheck source first~before~ReadmeCheck.cs -- source~ReadmeCheck.cs -- self-test~must come after
    the gate of the test job before the tests~before~SkipGate.cs -- check~- name: Test~must come after
    no retention days~drop~retention-days: 7~~needs a `retention-days:` of 1 to 14
    retention days of ninety~sub~retention-days: 7~retention-days: 90~needs a `retention-days:` of 1 to 14
    retention days that is no number~sub~retention-days: 7~retention-days: soon~needs a `retention-days:` of 1 to 14
    """;
