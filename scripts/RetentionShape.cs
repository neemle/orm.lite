#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
// Shape guard for .github/workflows/retention.yml and the script it runs, scripts/RunRetention.cs. The workflow
// deletes workflow runs and their artifacts, which is irreversible, so its reach is pinned to the narrowest one. Facts
// a green build must not lose: the triggers are exactly `workflow_run` (of the `ci` workflow, type `completed`, no
// branch filter), `schedule` (one daily cron) and `workflow_dispatch` with the inputs `dry_run` (boolean, default
// false: deletion is the default and a dry run an explicit opt-in) and `keep` (number, default 20), so no pull
// request, no push and no other workflow can start it; nothing gates the deletion (no repository variable, no
// RETENTION_APPLY); the permissions are exactly one block holding `actions: write` and `contents: read` (no job
// block widens it); a concurrency group with cancel-in-progress false keeps two runs from overlapping; one job on
// ubuntu-latest with a timeout and no job-level `if:`; first-party actions pinned by major version; the checkout takes
// no `ref`, no `repository` and no token and keeps no credentials, and no field of the triggering event is read (a
// workflow_run workflow runs with a write token, so nothing of the run that started it may reach it); the job runs
// the self-test of the script before the run of the script and nothing else (so no git command, no release call and
// no other `gh` call can be added); the step that runs the retention gets GH_TOKEN from github.token and the two
// inputs, and has no `if:`; no secret and no continue-on-error. The script keeps the default of 20, the minimum of 5,
// the one count over the runs of every workflow but the retention workflow, and the separate keep of 5 for the runs of
// the retention workflow (so they neither push a ci run out of the newest 20 nor grow), the filter that deletes
// completed runs only and never the current one, the allow-list of API calls, no mention of the old gate, and starts
// one process, `gh`.
//   dotnet run scripts/RetentionShape.cs -- check [<retention.yml> [<RunRetention.cs>]]   exit 0 pass, 1 shape failure
//   dotnet run scripts/RetentionShape.cs -- self-test    no files needed; every assertion has a negative case
// The reader knows the indentation of the workflow (job at 2, keys at 4, steps at 6, step keys at 8).
using System.Text.RegularExpressions;
using Case = (string Name, string Op, string A, string B, string Expect);

if (args.Length == 1 && args[0] == "self-test") return SelfTest();
if (args.Length is >= 1 and <= 3 && args[0] == "check") return Check(args[1..]);
Console.Error.WriteLine("usage: RetentionShape.cs check [<retention.yml> [<RunRetention.cs>]] | self-test");
return 2;

int Check(string[] given)
{
    var paths = new[] { ".github/workflows/retention.yml", "scripts/RunRetention.cs" };
    for (var i = 0; i < given.Length; i++) paths[i] = given[i];
    var missing = paths.Where(p => !File.Exists(p)).ToList();
    foreach (var p in missing) Console.Error.WriteLine("FAIL: the file " + p + " does not exist");
    if (missing.Count > 0) return 1;
    var fails = Judge(File.ReadAllText(paths[0]), File.ReadAllText(paths[1]));
    foreach (var message in fails) Console.Error.WriteLine("FAIL: " + message);
    if (fails.Count == 0) Console.WriteLine("PASS: the retention workflow keeps its narrow shape.");
    return fails.Count == 0 ? 0 : 1;
}

string SelfTestCommand() => "dotnet run scripts/RunRetention.cs -- self-test";

string RunCommand() => "dotnet run scripts/RunRetention.cs -- run";

string[] Forbidden() =>
[
    "secrets.", "vars.", "RETENTION_APPLY", "continue-on-error", "pull_request", "github.event.", "github.head_ref",
    "github.ref", "ref:", "repository:", "token:", "git push", "git tag", "git branch", "gh api", "gh release",
    "gh run", "gh repo", "contents: write", "write-all", "id-token",
];

List<string> Judge(string text, string script)
{
    var f = new List<string>();
    var lines = Lines(text);
    var on = Below(lines, "on:");
    var triggers = on.Where(l => Indent(l) == 2).Select(l => l.Trim()).Order(StringComparer.Ordinal).ToList();
    if (!triggers.SequenceEqual(new[] { "schedule:", "workflow_dispatch:", "workflow_run:" }))
        f.Add("the triggers must be exactly workflow_run, schedule and workflow_dispatch, they are " + Join(triggers));
    Finished(Below(on, "  workflow_run:"), f);
    Schedule(Below(on, "  schedule:"), f);
    Inputs(Below(Below(on, "  workflow_dispatch:"), "    inputs:"), f);
    Permissions(lines, f);
    Concurrency(lines, f);
    Job(lines, f);
    foreach (var l in lines)
        foreach (var token in Forbidden().Where(t => l.Contains(t, StringComparison.Ordinal)))
            f.Add("the workflow must not contain `" + token + "`: " + l.Trim());
    Script(script, f);
    return f;
}

// ---- reading: the lines of the file without blanks and comments, and the region below a header ----

int Indent(string line) => line.Length - line.TrimStart(' ').Length;

string Join(IEnumerable<string> items) => string.Join(" | ", items);

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

int Find(List<List<string>> steps, string command) => steps.FindIndex(s => Commands(s).Contains(command));

// ---- assertions on the triggers, the permissions and the concurrency ----

void Finished(List<string> body, List<string> f)
{
    var keys = body.Select(l => l.Trim()).Order(StringComparer.Ordinal).ToList();
    if (!keys.SequenceEqual(new[] { "types: [completed]", "workflows: [ci]" }))
        f.Add("the workflow_run trigger must be exactly `workflows: [ci]` and `types: [completed]`, it is "
            + Join(keys));
}

void Schedule(List<string> body, List<string> f)
{
    var crons = body.Select(l => l.Trim()).Where(l => l.StartsWith("- cron:", StringComparison.Ordinal)).ToList();
    if (crons.Count != 1 || !Regex.IsMatch(crons[0], "^- cron: \"\\d{1,2} \\d{1,2} \\* \\* \\*\"\\z"))
        f.Add("the schedule must be one daily entry `- cron: \"M H * * *\"`");
}

void Inputs(List<string> inputs, List<string> f)
{
    var names = inputs.Where(l => Indent(l) == 6).Select(l => l.Trim()).Order(StringComparer.Ordinal).ToList();
    if (!names.SequenceEqual(new[] { "dry_run:", "keep:" }))
        f.Add("the inputs must be exactly dry_run and keep, they are " + Join(names));
    Input(Below(inputs, "      dry_run:"), "dry_run", ("boolean", "false"), f);
    Input(Below(inputs, "      keep:"), "keep", ("number", "20"), f);
}

void Input(List<string> body, string name, (string Type, string Default) want, List<string> f)
{
    var keys = body.Select(l => l.Trim()).ToList();
    if (!keys.Contains("type: " + want.Type) || !keys.Contains("default: " + want.Default))
        f.Add("the input " + name + " must be `type: " + want.Type + "` with `default: " + want.Default + "`");
}

void Permissions(List<string> lines, List<string> f)
{
    var blocks = lines.Count(l => l.Trim().StartsWith("permissions:", StringComparison.Ordinal));
    var granted = Below(lines, "permissions:").Select(l => l.Trim()).Order(StringComparer.Ordinal).ToList();
    if (blocks != 1 || !granted.SequenceEqual(new[] { "actions: write", "contents: read" }))
        f.Add("the workflow must have exactly one permissions block holding `actions: write` and `contents: read`, "
            + "it has " + blocks + " and grants " + Join(granted));
}

void Concurrency(List<string> lines, List<string> f)
{
    var body = Below(lines, "concurrency:").Select(l => l.Trim()).ToList();
    var group = body.Any(l => l.StartsWith("group: ", StringComparison.Ordinal) && l.Length > 7);
    if (!group || !body.Contains("cancel-in-progress: false"))
        f.Add("the workflow needs `concurrency:` with a `group:` and `cancel-in-progress: false`, so that two runs "
            + "never overlap");
}

// ---- assertions on the job and its steps ----

void Job(List<string> lines, List<string> f)
{
    var ids = Below(lines, "jobs:").Where(l => Indent(l) == 2).Select(l => l.Trim()).ToList();
    if (ids.Count != 1)
    {
        f.Add("the workflow must hold exactly one job, it holds " + ids.Count);
        return;
    }
    var job = Below(lines, "  " + ids[0]);
    var keys = job.Where(l => Indent(l) == 4).Select(l => l.Trim()).ToList();
    if (!keys.Contains("runs-on: ubuntu-latest")) f.Add("the job lacks `runs-on: ubuntu-latest`");
    if (!keys.Any(k => Regex.IsMatch(k, @"^timeout-minutes: \d+\z")))
        f.Add("the job lacks a numeric `timeout-minutes:`");
    if (keys.Any(k => k.StartsWith("if:", StringComparison.Ordinal))) f.Add("the job has a job-level `if:`");
    Steps(StepsOf(job), f);
}

void Steps(List<List<string>> steps, List<string> f)
{
    var test = Find(steps, SelfTestCommand());
    var run = Find(steps, RunCommand());
    if (test < 0) f.Add("the job has no step running `" + SelfTestCommand() + "`");
    if (run < 0) f.Add("the job has no step running `" + RunCommand() + "`");
    else if (test > run) f.Add("the self-test step must come before the step that runs the retention");
    foreach (var step in steps) Step(step, f);
    foreach (var s in new[] { test, run }.Where(s => s >= 0))
        if (steps[s].Any(l => l.StartsWith("        if:", StringComparison.Ordinal)))
            f.Add("the step running `" + Commands(steps[s])[0] + "` has an `if:`, a step that can be skipped");
    if (run >= 0) RunStep(steps[run], f);
}

void Step(List<string> step, List<string> f)
{
    var uses = step.Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("uses:", StringComparison.Ordinal)) ?? "";
    if (uses.Length > 0 && !Regex.IsMatch(uses, @"^uses: actions/(checkout|setup-dotnet)@v\d+\z"))
        f.Add("`" + uses + "` is not actions/checkout or actions/setup-dotnet pinned as @v<digits>");
    if (uses.StartsWith("uses: actions/checkout", StringComparison.Ordinal)
        && !step.Any(l => l.Trim() == "persist-credentials: false"))
        f.Add("the checkout step must set `persist-credentials: false`");
    foreach (var c in Commands(step).Where(c => c != SelfTestCommand() && c != RunCommand()))
        f.Add("the job may run only the self-test and the run of scripts/RunRetention.cs, it runs `" + c + "`");
}

void RunStep(List<string> step, List<string> f)
{
    var lines = step.Select(l => l.Trim()).ToList();
    var needed = new[] { "GH_TOKEN: ${{ github.token }}", "RETENTION_KEEP: ${{ inputs.keep }}",
        "RETENTION_DRY_RUN: ${{ inputs.dry_run }}" };
    foreach (var need in needed.Where(n => !lines.Contains(n)))
        f.Add("the step that runs the retention lacks the env line `" + need + "`");
}

// ---- assertions on the script ----

void Script(string source, List<string> f)
{
    var needed = new[] { "private const int DefaultKeep = 20;", "private const int MinimumKeep = 5;",
        ".Where(r => r.Status == \"completed\" && r.Id != current)", "private const int OwnKeep = 5;",
        "Beyond(all.Where(r => r.Workflow != own), current, keep)",
        "Beyond(all.Where(r => r.Workflow == own), current, OwnKeep)", "if (!Allowed(method, path))",
        "new ProcessStartInfo(\"gh\")" };
    foreach (var token in needed.Where(t => !source.Contains(t, StringComparison.Ordinal)))
        f.Add("scripts/RunRetention.cs lacks `" + token + "`");
    if (source.Contains("RETENTION_APPLY", StringComparison.Ordinal))
        f.Add("scripts/RunRetention.cs must not mention RETENTION_APPLY: nothing gates the deletion");
    foreach (var token in new[] { "ProcessStartInfo(", "Process.Start(" })
        if (Regex.Matches(source, Regex.Escape(token)).Count != 1)
            f.Add("scripts/RunRetention.cs must start exactly one process, but uses `" + token + "` other than once");
}

// ---- self-test: a passing fixture, and one mutated negative per assertion ----

string Good() => """
name: retention
on:
  workflow_run:
    workflows: [ci]
    types: [completed]
  schedule:
    - cron: "17 3 * * *"
  workflow_dispatch:
    inputs:
      dry_run:
        description: Only list the runs that would be deleted, and delete nothing
        type: boolean
        default: false
      keep:
        description: How many of the newest runs to keep (at least 5)
        type: number
        default: 20
permissions:
  actions: write
  contents: read
concurrency:
  group: retention
  cancel-in-progress: false
jobs:
  retention:
    name: retention
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - uses: actions/checkout@v7
        with:
          persist-credentials: false
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: 10.0.x
      - run: dotnet run scripts/RunRetention.cs -- self-test
      - name: List
        env:
          GH_TOKEN: ${{ github.token }}
          RETENTION_KEEP: ${{ inputs.keep }}
          RETENTION_DRY_RUN: ${{ inputs.dry_run }}
        run: dotnet run scripts/RunRetention.cs -- run
""" + "\n";

string GoodScript() => """
private const int DefaultKeep = 20;
private const int MinimumKeep = 5;
private const int OwnKeep = 5;
Beyond(all.Where(r => r.Workflow != own), current, keep)
    .Concat(Beyond(all.Where(r => r.Workflow == own), current, OwnKeep));
var doomed = runs.Where(r => r.Status == "completed" && r.Id != current);
if (!Allowed(method, path)) throw new InvalidOperationException();
var start = new ProcessStartInfo("gh");
using var process = Process.Start(start);
""" + "\n";

string Apply(string text, string op, string a, string b)
{
    if (op == "sub") return text.Replace(a, b, StringComparison.Ordinal);
    var lines = text.Split('\n').ToList();
    var at = lines.FindIndex(l => l.Contains(a, StringComparison.Ordinal));
    if (at < 0) return text;
    if (op == "drop") return string.Join('\n', lines.Where(l => !l.Contains(a, StringComparison.Ordinal)));
    if (op == "ins") lines.Insert(at + 1, b);
    if (op == "after")
    {
        var moved = lines[at];
        lines.RemoveAt(at);
        lines.Insert(lines.FindLastIndex(l => l.Trim().Length > 0) + 1, moved);
    }
    return string.Join('\n', lines);
}

(string Workflow, string Script) Mutate(Case c)
{
    if (!c.Op.EndsWith("@script", StringComparison.Ordinal)) return (Apply(Good(), c.Op, c.A, c.B), GoodScript());
    return (Good(), Apply(GoodScript(), c.Op[..^7], c.A, c.B));
}

bool Holds(Case c)
{
    var (workflow, script) = Mutate(c);
    var fails = Judge(workflow, script);
    var changed = workflow != Good() || script != GoodScript();
    var ok = changed && fails.Any(m => m.Contains(c.Expect, StringComparison.Ordinal));
    if (!ok)
        Console.Error.WriteLine($"SELF-TEST FAIL: {c.Name}: expected `{c.Expect}`, applied: {changed}, got: "
            + (fails.Count == 0 ? "none" : Join(fails)));
    return ok;
}

int SelfTest()
{
    var good = Judge(Good(), GoodScript());
    if (good.Count > 0) Console.Error.WriteLine("SELF-TEST FAIL: the fixture is refused: " + Join(good));
    var cases = Cases();
    var failed = cases.Count(c => !Holds(c)) + (good.Count > 0 ? 1 : 0);
    Console.WriteLine("self-test: 1 passing fixture and " + cases.Count + " mutated cases, " + failed + " failed");
    return failed == 0 ? 0 : 1;
}

// a case is name ~ operation ~ operand a ~ operand b ~ expected failure. The operations: sub (replace a by b, all
// occurrences), drop (remove the lines with a), ins (add the line b after the first line with a) and after (move the
// first line with a behind the last line); an operation ending in @script applies to the script fixture; \n in an
// operand is a line break.
List<Case> Cases() =>
    (TriggerCases() + "\n" + ShapeCases() + "\n" + StepCases() + "\n" + ScriptCases())
        .Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('~'))
        .Select(p => (p[0].Trim(), p[1], p[2].Replace("\\n", "\n"), p[3].Replace("\\n", "\n"), p[4])).ToList();

string TriggerCases() => """
    a push trigger~sub~  schedule:~  push:\n  schedule:~triggers must be exactly
    a pull_request trigger~sub~  schedule:~  pull_request:\n  schedule:~triggers must be exactly
    a pull_request_target trigger~sub~  schedule:~  pull_request_target:\n  schedule:~must not contain `pull_request`
    no schedule~sub~  schedule:~  other:~triggers must be exactly
    no manual trigger~sub~  workflow_dispatch:~  other:~triggers must be exactly
    no workflow_run trigger~sub~  workflow_run:~  other:~triggers must be exactly
    the workflow_run of another workflow~sub~workflows: [ci]~workflows: [other]~workflow_run trigger must be exactly
    the workflow_run of two workflows~sub~workflows: [ci]~workflows: [ci, other]~workflow_run trigger must be exactly
    the workflow_run of every workflow~drop~workflows: [ci]~~workflow_run trigger must be exactly
    a workflow_run type requested~sub~types: [completed]~types: [requested]~workflow_run trigger must be exactly
    a workflow_run type added~sub~types: [completed]~types: [completed, requested]~workflow_run trigger must be exactly
    no workflow_run type~drop~types: [completed]~~workflow_run trigger must be exactly
    a workflow_run branch filter~ins~types: [completed]~    branches: [main]~workflow_run trigger must be exactly
    an hourly cron~sub~"17 3 * * *"~"17 * * * *"~one daily entry
    a weekly cron~sub~"17 3 * * *"~"17 3 * * 1"~one daily entry
    two crons~ins~- cron:~    - cron: "5 4 * * *"~one daily entry
    dry_run defaults to true~sub~default: false~default: true~input dry_run must be
    dry_run has no default~drop~default: false~~input dry_run must be
    dry_run is no boolean~sub~type: boolean~type: string~input dry_run must be
    keep defaults to 50~sub~default: 20~default: 50~input keep must be
    keep is a string~sub~type: number~type: string~input keep must be
    a third input~sub~      keep:~      extra:\n        type: string\n      keep:~inputs must be exactly
    no inputs~sub~    inputs:~    other:~inputs must be exactly
    """;

string ShapeCases() => """
    contents write~sub~contents: read~contents: write~exactly one permissions block
    an extra permission~ins~  actions: write~  issues: write~exactly one permissions block
    actions read only~sub~actions: write~actions: read~exactly one permissions block
    no permissions~sub~permissions:\n  actions: write\n  contents: read\n~~exactly one permissions block
    a job permissions block~ins~    timeout-minutes: 15~    permissions:\n      id-token: write~exactly one permissions
    no concurrency~sub~concurrency:\n  group: retention\n  cancel-in-progress: false\n~~needs `concurrency:`
    cancel in progress~sub~cancel-in-progress: false~cancel-in-progress: true~needs `concurrency:`
    no group~sub~  group: retention\n~~needs `concurrency:`
    a second job~sub~jobs:\n~jobs:\n  other:\n    runs-on: ubuntu-latest\n~exactly one job
    a self-hosted runner~sub~runs-on: ubuntu-latest~runs-on: self-hosted~lacks `runs-on: ubuntu-latest`
    no timeout~drop~timeout-minutes~~numeric `timeout-minutes:`
    a job-level if~ins~    timeout-minutes: 15~    if: always()~job-level `if:`
    a secret~sub~github.token~secrets.PAT~must not contain `secrets.`
    a repository variable~sub~github.token~vars.PAT~must not contain `vars.`
    the apply gate in the workflow~ins~RETENTION_DRY_RUN:~          RETENTION_APPLY: yes~not contain `RETENTION_APPLY`
    the apply gate from a variable~ins~RETENTION_DRY_RUN:~          X: ${{ vars.X }}~not contain `vars.`
    a field of the triggering event~sub~inputs.keep~github.event.workflow_run.head_branch~not contain `github.event.`
    the head branch of the ci run~sub~inputs.keep~github.head_ref~not contain `github.head_ref`
    """;

string StepCases() => """
    a checkout of another ref~ins~persist-credentials: false~          ref: x~must not contain `ref:`
    a checkout of another repository~ins~persist-credentials: false~          repository: a/b~`repository:`
    a token for the checkout~ins~persist-credentials: false~          token: x~`token:`
    credentials persisted~sub~persist-credentials: false~persist-credentials: true~must set `persist-credentials: false`
    a third-party action~sub~actions/setup-dotnet@v6~other/setup-dotnet@v4~pinned as @v<digits>
    an action on a branch~sub~actions/checkout@v7~actions/checkout@main~pinned as @v<digits>
    no self-test step~drop~RunRetention.cs -- self-test~~no step running `dotnet run scripts/RunRetention.cs -- self
    self-test after the run~after~RunRetention.cs -- self-test~~must come before
    no run step~sub~RunRetention.cs -- run~RunRetention.cs -- go~no step running `dotnet run scripts/RunRet
    another script~sub~scripts/RunRetention.cs -- run~scripts/Other.cs -- run~may run only
    an extra command~ins~RunRetention.cs -- self-test~      - run: echo hi~may run only
    a branch deletion~ins~RunRetention.cs -- self-test~      - run: git push origin --delete x~not contain `git push`
    a release deletion~ins~RunRetention.cs -- self-test~      - run: gh release delete v1~must not contain `gh release`
    a direct api call~ins~RunRetention.cs -- self-test~      - run: gh api -X DELETE x~must not contain `gh api`
    no token~drop~GH_TOKEN:~~lacks the env line `GH_TOKEN
    keep not passed~drop~RETENTION_KEEP:~~lacks the env line `RETENTION_KEEP
    dry run not passed~sub~RETENTION_DRY_RUN: ${{ inputs.dry_run }}~RETENTION_DRY_RUN: false~env line `RETENTION_DRY
    an if on the run step~ins~RunRetention.cs -- run~        if: always()~has an `if:`
    an if on the self-test~ins~RunRetention.cs -- self-test~        if: false~has an `if:`
    continue-on-error~ins~RunRetention.cs -- self-test~        continue-on-error: true~not contain `continue-on-error`
    """;

string ScriptCases() => """
    the allow-list bypassed~sub@script~if (!Allowed(method, path))~if (false)~lacks `if (!Allowed(method, path))`
    another program~sub@script~new ProcessStartInfo("gh")~new ProcessStartInfo("git")~lacks `new ProcessStartInfo("gh")`
    a second program~ins@script~Process.Start(~var other = new ProcessStartInfo("git");~must start exactly one process
    the apply gate in the script~ins@script~DefaultKeep~// RETENTION_APPLY~must not mention RETENTION_APPLY
    the retention runs counted with the others~sub@script~r.Workflow != own~true~lacks `Beyond(all.Where(r => r.Workfl
    the retention runs not trimmed~sub@script~r.Workflow == own~false~lacks `Beyond(all.Where(r => r.Workflow == own)
    the retention runs kept as many as keep~sub@script~current, OwnKeep~current, keep~lacks `Beyond(all.Where(r => r
    a larger retention keep~sub@script~OwnKeep = 5~OwnKeep = 20~lacks `private const int OwnKeep = 5;`
    no retention keep~drop@script~OwnKeep = 5~~lacks `private const int OwnKeep = 5;`
    a low minimum~sub@script~MinimumKeep = 5~MinimumKeep = 1~lacks `private const int MinimumKeep = 5;`
    another default~sub@script~DefaultKeep = 20~DefaultKeep = 50~lacks `private const int DefaultKeep = 20;`
    no completed filter~sub@script~r.Status == "completed"~true~lacks `.Where(
    no current run filter~sub@script~r.Id != current~true~lacks `.Where(
    """;
