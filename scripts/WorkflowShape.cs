#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
// Workflow-shape guard for .github/workflows/ci.yml, tests/compose.yaml and demo/compose.yaml. The audit finding behind
// it (B54) is that a green build hid skipped PostgreSQL tests; this guard fails when an edit drops the compose step,
// the variable, the logger or the gate step, loosens either compose file (image tag, exec-form health check, every
// port on loopback, no network_mode, tmpfs), or the supply-chain posture of the workflow (triggers, permissions,
// action pins, runner class, no ORMLITE_TEST_PG_* override of the image policy).
//   dotnet run scripts/WorkflowShape.cs -- check [<ci.yml> [<tests-compose> [<demo-compose>]]]
//                                                               exit 0 pass, 1 shape failure, 2 usage
//   dotnet run scripts/WorkflowShape.cs -- self-test          no workflow needed; every assertion has a negative case
// scripts/StepShape.cs guards the zone step and the thread cap of the test step; this one requires its two steps.
// The reader understands the block-YAML subset ci.yml uses (no anchors, no flow style) and fails closed on the rest.
using Case = (string Name, string Kind, string A, string B, string Expect);
using Node = (int Line, int Col, string Key, string Value, int Parent);

if (args.Length == 1 && args[0] == "self-test") return SelfTest();
if (args.Length is >= 1 and <= 4 && args[0] == "check") return Check(args[1..]);
Console.Error.WriteLine("usage: WorkflowShape.cs check [<ci.yml> [<tests-compose> [<demo-compose>]]] | self-test");
return 2;

string Root()
{
    for (var d = new DirectoryInfo(Directory.GetCurrentDirectory()); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "Neemle.Orm.Lite.slnx"))) return d.FullName;
    throw new InvalidOperationException("Neemle.Orm.Lite.slnx not found above the current directory");
}

int Check(string[] given)
{
    var root = Root();
    var paths = new[] { ".github/workflows/ci.yml", "tests/compose.yaml", "demo/compose.yaml" }
        .Select(p => Path.Combine(root, p)).ToArray();
    for (var i = 0; i < given.Length; i++) paths[i] = given[i];
    var missing = paths.Where(p => !File.Exists(p)).ToList();
    foreach (var p in missing) Console.Error.WriteLine("FAIL: the file " + p + " does not exist");
    if (missing.Count > 0) return 1;
    var t = paths.Select(p => File.ReadAllText(p)).ToList();
    var fails = Judge(t[0], t[1], t[2]);
    foreach (var message in fails) Console.Error.WriteLine("FAIL: " + message);
    if (fails.Count == 0) Console.WriteLine("PASS: the workflow keeps the compose step, the variable and the gate.");
    return fails.Count == 0 ? 0 : 1;
}

List<string> Judge(string text, string compose, string demoCompose)
{
    var f = new List<string>();
    Compose(compose, "tests/compose.yaml", ("${ORMLITE_TEST_PG_IMAGE:-neemle/pg.lite:", "}"), f);
    Compose(demoCompose, "demo/compose.yaml", ("neemle/pg.lite:", ""), f);
    var n = Parse(text, f);
    Duplicates(n, f);
    Triggers(n, f);
    Permissions(n, f);
    Uses(n, f);
    foreach (var line in text.Split('\n').Where(l => !l.TrimStart().StartsWith('#')))
        if (line.Contains("secrets.") || line.Contains("secrets["))
            f.Add("the workflow references a secret: " + line.Trim());
    var jobs = Kid(n, 0, "jobs");
    var test = Kid(n, jobs, "test");
    if (test < 0) f.Add("there is no job `test` under `jobs`");
    else TestJob(n, test, f);
    DemoPostgres(n, Kid(n, jobs, "demo-postgres"), f);
    return f;
}

// ---- reading: an indentation tree of (line, column, key, value, parent); node 0 is the root, a list item is "-" ----

List<Node> Parse(string text, List<string> f)
{
    var n = new List<Node> { (0, -1, "", "", -1) };
    var stack = new List<int> { 0 };
    var block = -1;
    var no = 0;
    foreach (var raw in text.Split('\n'))
    {
        no++;
        var line = raw.TrimEnd('\r');
        if (line.Trim().Length == 0) continue;
        if (block >= 0 && line.Length - line.TrimStart(' ').Length > n[block].Col)
        {
            var b = n[block];
            n[block] = (b.Line, b.Col, b.Key, b.Value + (b.Value.Length > 0 ? "\n" : "") + line.Trim(), b.Parent);
            continue;
        }
        block = ReadLine(n, stack, no, line, f);
    }
    return n;
}

int ReadLine(List<Node> n, List<int> stack, int no, string line, List<string> f)
{
    var trimmed = line.TrimStart(' ', '\t');
    var lead = line[..(line.Length - trimmed.Length)];
    if (lead.Contains('\t')) f.Add($"line {no}: a TAB in the indentation");
    if (lead.Contains('\t') || trimmed.StartsWith('#')) return -1;
    if (trimmed != "-" && !trimmed.StartsWith("- ", StringComparison.Ordinal))
        return ReadKey(n, stack, (no, lead.Length), trimmed, f);
    var item = Add(n, stack, (no, lead.Length), "-", "");
    var rest = trimmed[1..].TrimStart();
    if (rest.StartsWith("- ", StringComparison.Ordinal)) f.Add($"line {no}: a nested list item cannot be read");
    if (rest.Length == 0) return -1;
    if (Split(rest) is not null) return ReadKey(n, stack, (no, lead.Length + trimmed.Length - rest.Length), rest, f);
    n[item] = (no, lead.Length, "-", Unquote(rest), n[item].Parent);
    return -1;
}

int ReadKey(List<Node> n, List<int> stack, (int No, int Col) at, string text, List<string> f)
{
    var pair = Split(text);
    if (pair is null)
    {
        f.Add($"line {at.No}: cannot be read as `key: value`: {text}");
        return -1;
    }
    var (key, value) = pair.Value;
    var isBlock = value.StartsWith('|') || value.StartsWith('>');
    var index = Add(n, stack, at, key, isBlock ? "" : value);
    return isBlock ? index : -1;
}

int Add(List<Node> n, List<int> stack, (int No, int Col) at, string key, string value)
{
    while (n[stack[^1]].Col >= at.Col) stack.RemoveAt(stack.Count - 1);
    n.Add((at.No, at.Col, key, value, stack[^1]));
    stack.Add(n.Count - 1);
    return n.Count - 1;
}

(string Key, string Value)? Split(string rest)
{
    var i = rest.IndexOf(": ", StringComparison.Ordinal);
    var key = i >= 0 ? rest[..i] : rest.EndsWith(':') ? rest[..^1] : "";
    if (key.Length == 0 || !key.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.')) return null;
    return (key, i >= 0 ? Unquote(rest[(i + 2)..].Trim()) : "");
}

string Unquote(string v) => v.Length >= 2 && v[0] == v[^1] && v[0] is '"' or '\'' ? v[1..^1] : v;

// ---- queries: a missing node is -1 and every query on it finds nothing ----

int Kid(List<Node> n, int parent, string key) => parent < 0 ? -1 : n.FindIndex(x => x.Parent == parent && x.Key == key);

List<int> Kids(List<Node> n, int parent) =>
    parent < 0 ? [] : Enumerable.Range(0, n.Count).Where(i => n[i].Parent == parent).ToList();

List<int> Under(List<Node> n, int root)
{
    var found = new List<int>();
    var todo = new Stack<int>(Kids(n, root));
    while (todo.Count > 0)
    {
        found.Add(todo.Pop());
        foreach (var k in Kids(n, found[^1])) todo.Push(k);
    }
    return found;
}

string Text(List<Node> n, int i) => i < 0 ? "" : n[i].Value.Replace('\n', ' ');

string Run(List<Node> n, int step) => Text(n, Kid(n, step, "run"));

// ---- assertions on the whole file ----

void Duplicates(List<Node> n, List<string> f)
{
    foreach (var g in n.GroupBy(x => (x.Parent, x.Key)).Where(g => g.Count() > 1 && g.Key.Key != "-"))
        f.Add($"line {g.Skip(1).First().Line}: the key `{g.Key.Key}` appears twice under one parent");
}

void Triggers(List<Node> n, List<string> f)
{
    var on = Kid(n, 0, "on");
    if (on < 0) f.Add("the workflow has no `on:` trigger block");
    var names = on >= 0 && n[on].Value.Length > 0 ? [n[on].Value] : Kids(n, on).Select(i => n[i].Key).ToList();
    foreach (var name in names.Where(name => name is not ("push" or "pull_request")))
        f.Add("the trigger `" + name + "` is not allowed (only push and pull_request)");
}

void Permissions(List<Node> n, List<string> f)
{
    if (Kid(n, 0, "permissions") < 0) f.Add("the workflow has no top-level `permissions:` block");
    foreach (var i in Enumerable.Range(0, n.Count).Where(i => n[i].Key == "permissions"))
    {
        var values = n[i].Value.Length > 0 ? [n[i].Value] : Kids(n, i).Select(k => n[k].Value).ToList();
        foreach (var v in values.Where(v => v is not ("read" or "none" or "read-all")))
            f.Add($"line {n[i].Line}: the permission value `{v}` is not read");
    }
}

void Uses(List<Node> n, List<string> f)
{
    foreach (var x in n.Where(x => x.Key == "uses" && !FirstParty(x.Value)))
        f.Add($"line {x.Line}: `uses: {x.Value}` is not a first-party action pinned as actions/<name>@v<digits>");
}

bool FirstParty(string v)
{
    var at = v.IndexOf('@');
    if (at < 0 || v.IndexOf('@', at + 1) >= 0 || !v.StartsWith("actions/", StringComparison.Ordinal)) return false;
    var name = v[8..at];
    var named = name.Length > 0 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    return named && IsMajor(v[(at + 1)..]);
}

bool IsMajor(string tag) => tag.Length > 1 && tag[0] == 'v' && tag[1..].All(char.IsAsciiDigit);

// ---- assertions on tests/compose.yaml and demo/compose.yaml, the servers the CI jobs start ----

void Compose(string text, string label, (string Prefix, string Suffix) form, List<string> f)
{
    var n = Parse(text, f);
    var pg = Kid(n, Kid(n, 0, "services"), "postgres");
    if (pg < 0)
    {
        f.Add(label + " has no services.postgres");
        return;
    }
    var image = Text(n, Kid(n, pg, "image"));
    if (!MajorLine(image, form))
        f.Add(label + " image `" + image + "` is not " + form.Prefix + "<major>" + form.Suffix);
    var check = Text(n, Kid(n, Kid(n, pg, "healthcheck"), "test"));
    if (!check.StartsWith("[\"CMD\",", StringComparison.Ordinal) || !check.Contains("--healthcheck"))
        f.Add(label + " health check is not the exec form of postgres --healthcheck (no shell)");
    var ports = Kids(n, Kid(n, pg, "ports")).Select(i => n[i].Value).ToList();
    if (ports.Count == 0 || !ports.All(v => v.StartsWith("127.0.0.1:", StringComparison.Ordinal)))
        f.Add(label + " does not publish every port on 127.0.0.1 only");
    if (Kid(n, pg, "network_mode") >= 0) f.Add(label + " must not set network_mode (loopback bypass)");
    if (Kid(n, pg, "tmpfs") < 0) f.Add(label + " keeps its data in no tmpfs");
}

// ---- assertions on job `test` and job `demo-postgres` ----

void TestJob(List<Node> n, int job, List<string> f)
{
    var timeout = Text(n, Kid(n, job, "timeout-minutes"));
    if (timeout.Length == 0 || !timeout.All(char.IsAsciiDigit)) f.Add("job test has no numeric timeout-minutes");
    var runsOn = Kid(n, job, "runs-on");
    var labels = Text(n, runsOn) + string.Concat(Under(n, runsOn).Select(i => n[i].Value));
    if (runsOn < 0) f.Add("job test has no runs-on");
    if (labels.Contains("self-hosted", StringComparison.OrdinalIgnoreCase))
        f.Add("job test runs on a self-hosted runner (trust authentication is accepted on hosted runners only)");
    if (Kid(n, job, "needs") >= 0) f.Add("job test must not have `needs:`");
    foreach (var i in Under(n, job).Where(i => n[i].Key == "continue-on-error"))
        f.Add($"line {n[i].Line}: continue-on-error inside job test");
    Steps(n, job, f);
    Variable(n, job, f);
}

// a major-line tag only: the prefix, digits, the suffix; no patch, no digest, never latest
bool MajorLine(string image, (string Prefix, string Suffix) form) =>
    image.StartsWith(form.Prefix, StringComparison.Ordinal) && image.EndsWith(form.Suffix, StringComparison.Ordinal)
    && image.Length > form.Prefix.Length + form.Suffix.Length
    && image[form.Prefix.Length..^form.Suffix.Length].All(char.IsAsciiDigit);

int Find(List<Node> n, List<int> steps, string token) =>
    steps.FindIndex(s => Run(n, s).Contains(token, StringComparison.Ordinal));

void Steps(List<Node> n, int job, List<string> f)
{
    var steps = Kids(n, Kid(n, job, "steps"));
    var order = new[] { "SkipGate.cs -- self-test", "WorkflowShape.cs -- self-test", "WorkflowShape.cs -- check",
        "StepShape.cs -- self-test", "StepShape.cs -- check", "dotnet build", "-f tests/compose.yaml up -d --wait",
        "dotnet test", "SkipGate.cs -- check" };
    var at = order.Select(token => Find(n, steps, token)).ToArray();
    for (var k = 0; k < order.Length; k++)
        if (at[k] < 0) f.Add("job test has no step running `" + order[k] + "`");
        else if (k > 0 && at[k - 1] >= at[k])
            f.Add("the step `" + order[k] + "` must come after `" + order[k - 1] + "`");
    foreach (var s in at.Where(s => s >= 0).Select(s => steps[s]))
        if (Text(n, Kid(n, s, "if")) is { Length: > 0 } c && c != "success()")
            f.Add($"line {n[s].Line}: a step has `if: {c}`");
    if (at[7] >= 0) TestStep(n, steps[at[7]], f);
    if (at[8] >= 0) GateStep(Run(n, steps[at[8]]), f);
}

void TestStep(List<Node> n, int step, List<string> f)
{
    var value = Text(n, Kid(n, Kid(n, step, "env"), "ORMLITE_TEST_POSTGRES"));
    if (!value.StartsWith("Host=", StringComparison.Ordinal))
        f.Add("the test step does not set ORMLITE_TEST_POSTGRES to a non-blank Host=... value at step level");
    var run = Run(n, step);
    foreach (var token in new[] { "--no-build", "trx;LogFileName=tests.trx", "--results-directory TestResults",
        "--blame-crash", "--blame-hang-timeout" })
        if (!run.Contains(token, StringComparison.Ordinal)) f.Add("the test step lacks `" + token + "`");
    if (run.Contains("--filter", StringComparison.Ordinal)) f.Add("the test step must not use --filter");
}

void GateStep(string run, List<string> f)
{
    foreach (var token in new[] { "check TestResults/tests.trx", "tests/skip-allowlist.txt", "--require-postgres" })
        if (!run.Contains(token, StringComparison.Ordinal)) f.Add("the gate step lacks `" + token + "`");
}

void Variable(List<Node> n, int job, List<string> f)
{
    var steps = Kids(n, Kid(n, job, "steps"));
    var test = Find(n, steps, "dotnet test");
    foreach (var i in Enumerable.Range(0, n.Count).Where(i => n[i].Key == "ORMLITE_TEST_POSTGRES"))
        if (n[n[i].Parent].Key != "env" || test < 0 || n[n[i].Parent].Parent != steps[test])
            f.Add($"line {n[i].Line}: ORMLITE_TEST_POSTGRES belongs on the env of the test step of job test only");
    foreach (var x in n.Where(x => x.Key.StartsWith("ORMLITE_TEST_PG_", StringComparison.Ordinal)))
        f.Add($"line {x.Line}: {x.Key} must not be set in ci.yml (the defaults of tests/compose.yaml are the policy)");
}

void DemoPostgres(List<Node> n, int job, List<string> f)
{
    if (job < 0) f.Add("there is no job `demo-postgres` under `jobs`");
    var steps = Kids(n, Kid(n, job, "steps"));
    var up = Find(n, steps, "-f demo/compose.yaml up -d --wait");
    var demo = Find(n, steps, "grep -qx 'demo ok'");
    if (job >= 0 && up < 0) f.Add("job demo-postgres has no step running `-f demo/compose.yaml up -d --wait`");
    if (job >= 0 && demo < 0) f.Add("job demo-postgres no longer checks `grep -qx 'demo ok'`");
}

// ---- self-test: a passing fixture, one mutated negative per assertion, and positive controls for the reader ----

string FxTest() => """
  test:
    name: test (.NET ${{ matrix.dotnet }})
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
      - name: Check the gate
        run: dotnet run scripts/SkipGate.cs -- self-test
      - run: dotnet run scripts/WorkflowShape.cs -- self-test
      - run: dotnet run scripts/WorkflowShape.cs -- check
      - run: dotnet run scripts/StepShape.cs -- self-test
      - run: dotnet run scripts/StepShape.cs -- check
      - run: dotnet build Neemle.Orm.Lite.slnx -c Release
      - run: docker compose -f tests/compose.yaml up -d --wait
      - name: Test
        env:
          ORMLITE_TEST_POSTGRES: Host=127.0.0.1;Port=55433;Username=postgres
        run: >-
          dotnet test Neemle.Orm.Lite.slnx -c Release --no-build --logger "trx;LogFileName=tests.trx"
          --results-directory TestResults --blame-crash --blame-hang-timeout 10m
      - run: dotnet run scripts/SkipGate.cs -- check TestResults/tests.trx tests/skip-allowlist.txt --require-postgres w
""" + "\n";

string FxAot() => """
  aot-smoke:
    name: aot-smoke (.NET ${{ matrix.dotnet }})
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      matrix:
        include:
          - dotnet: 10.0.x
            tfm: net10.0
    steps:
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ matrix.dotnet }}
      - run: dotnet publish tests/AotSmoke -p:AotSmokeTfm=${{ matrix.tfm }}
""" + "\n";

string FxCompose() => """
name: orm-lite-tests
services:
  postgres:
    image: ${ORMLITE_TEST_PG_IMAGE:-neemle/pg.lite:19}
    environment:
      POSTGRES_HOST_AUTH_METHOD: trust
    ports:
      - "127.0.0.1:${ORMLITE_TEST_PG_PORT:-55433}:5432"
    tmpfs:
      - ${ORMLITE_TEST_PG_TMPFS:-/var/lib/postgresql/data:uid=70,gid=70,mode=0700}
    healthcheck:
      test: ["CMD", "/usr/local/pgsql/bin/postgres", "--healthcheck"]
""" + "\n";

string FxDemoCompose() => """
name: orm-lite-demo
services:
  postgres:
    image: neemle/pg.lite:19
    environment:
      POSTGRES_HOST_AUTH_METHOD: trust
    ports:
      - "127.0.0.1:55432:5432"
    tmpfs:
      - /var/lib/postgresql/data:uid=70,gid=70,mode=0700
    healthcheck:
      test: ["CMD", "/usr/local/pgsql/bin/postgres", "--healthcheck"]
""" + "\n";

string Good() => """
name: ci
on:
  push:
  pull_request:
permissions:
  contents: read
env:
  DOTNET_NOLOGO: "1"
jobs:
""" + "\n" + FxTest() + FxAot() + """
  demo-postgres:
    runs-on: ubuntu-latest
    steps:
      - run: docker compose -f demo/compose.yaml up -d --wait
      - run: |
          dotnet run --project demo | tee demo.log
          grep -qx 'demo ok' demo.log
""" + "\n";

string Apply(string part, string op, string a, string b)
{
    var lines = part.Split('\n').ToList();
    var at = lines.FindIndex(l => l.Contains(a, StringComparison.Ordinal));
    if (op == "sub") return part.Replace(a, b, StringComparison.Ordinal);
    if (at < 0) return part;
    if (op == "drop") return string.Join('\n', lines.Where(l => !l.Contains(a, StringComparison.Ordinal)));
    if (op == "ins") lines.Insert(at + 1, b);
    if (op == "cut") lines.RemoveRange(at, lines.FindIndex(at + 1, l => l.Contains(b, StringComparison.Ordinal)) - at);
    if (op == "before")
    {
        var moved = lines[at];
        lines.RemoveAt(at);
        lines.Insert(lines.FindIndex(l => l.Contains(b, StringComparison.Ordinal)), moved);
    }
    return string.Join('\n', lines);
}

string Mutate(string text, Case c)
{
    var demo = c.Kind.EndsWith("@demo", StringComparison.Ordinal);
    var cut = demo ? text.IndexOf("  demo-postgres:", StringComparison.Ordinal) : 0;
    return text[..cut] + Apply(text[cut..], demo ? c.Kind[..^5] : c.Kind, c.A, c.B);
}

(string Workflow, string Compose, string Demo) Fixtures(Case c)
{
    var kind = c.Kind;
    if (kind.EndsWith("@compose", StringComparison.Ordinal))
        return (Good(), Apply(FxCompose(), kind[..^8], c.A, c.B), FxDemoCompose());
    if (kind.EndsWith("@dcompose", StringComparison.Ordinal))
        return (Good(), FxCompose(), Apply(FxDemoCompose(), kind[..^9], c.A, c.B));
    return (Mutate(Good(), c), FxCompose(), FxDemoCompose());
}

bool Holds(Case c)
{
    var (workflow, compose, demo) = Fixtures(c);
    var fails = Judge(workflow, compose, demo);
    var changed = (workflow, compose, demo) != (Good(), FxCompose(), FxDemoCompose());
    var ok = changed && (c.Expect.Length == 0 ? fails.Count == 0 : fails.Any(m => m.Contains(c.Expect)));
    if (!ok)
        Console.Error.WriteLine($"SELF-TEST FAIL: {c.Name}: expected `{c.Expect}`, applied: {changed}, got: "
            + (fails.Count == 0 ? "none" : string.Join(" | ", fails)));
    return ok;
}

int SelfTest()
{
    var good = Judge(Good(), FxCompose(), FxDemoCompose());
    if (good.Count > 0) Console.Error.WriteLine("SELF-TEST FAIL: the fixture is refused: " + string.Join(" | ", good));
    var cases = ServiceCases().Concat(StepCases()).Concat(PostureCases()).Concat(ReaderCases()).ToList();
    var failed = cases.Count(c => !Holds(c)) + (good.Count > 0 ? 1 : 0);
    Console.WriteLine("self-test: 1 passing fixture and " + cases.Count + " mutated cases, " + failed + " failed");
    return failed == 0 ? 0 : 1;
}

// a case is name ~ operation ~ operand a ~ operand b ~ expected failure. The operations: sub (replace a by b), drop
// (remove the lines with a), ins (add the line b after the line with a), cut (remove from the line with a up to the
// line with b) and before (move the line with a before the line with b); @demo limits one to the demo-postgres job,
// @compose applies it to the tests compose fixture and @dcompose to the demo compose fixture.
List<Case> Cases(string table) =>
    table.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('~'))
        .Select(p => (p[0], p[1], Unescape(p[2]), Unescape(p[3]), p[4])).ToList();

string Unescape(string text) => text.Replace("\\n", "\n").Replace("\\t", "\t");

List<Case> ServiceCases() => Cases("""
    no compose step~drop~-f tests/compose.yaml up~~has no step running `-f tests/compose.yaml up -d --wait`
    compose step after the build~before~-f tests/compose.yaml up~dotnet build~must come after `dotnet build`
    compose image with a patch~sub@compose~pg.lite:19~pg.lite:19.1~is not ${ORMLITE_TEST_PG_IMAGE:-neemle
    compose image latest~sub@compose~pg.lite:19~pg.lite:latest~is not ${ORMLITE_TEST_PG_IMAGE:-neemle
    compose image back to postgres~sub@compose~neemle/pg.lite:19~postgres:18-alpine~is not ${ORMLITE_TEST_PG_IMAGE
    compose health check in shell form~sub@compose~["CMD",~["CMD-SHELL",~exec form of postgres --healthcheck
    compose health check without the flag~sub@compose~--healthcheck~--version~exec form of postgres --healthcheck
    compose port on every interface~sub@compose~"127.0.0.1:~"0.0.0.0:~every port on 127.0.0.1 only
    a second compose port on every interface~ins@compose~"127.0.0.1:~      - "0.0.0.0:9:9"~every port on 127.0.0.1
    compose with host networking~ins@compose~    image: ${ORMLITE~    network_mode: host~must not set network_mode
    image env on the compose step~ins~compose.yaml up~        env:\n          ORMLITE_TEST_PG_IMAGE: x~must not be set
    compose without tmpfs~sub@compose~tmpfs:~volumes:~keeps its data in no tmpfs
    compose without a postgres service~sub@compose~  postgres:~  other:~tests/compose.yaml has no services.postgres
    demo compose image with a patch~sub@dcompose~pg.lite:19~pg.lite:19.1~demo/compose.yaml image
    demo compose image latest~sub@dcompose~pg.lite:19~pg.lite:latest~is not neemle/pg.lite:<major>
    demo compose health check in shell form~sub@dcompose~["CMD",~["CMD-SHELL",~demo/compose.yaml health check
    demo compose port on every interface~sub@dcompose~"127.0.0.1:~"0.0.0.0:~demo/compose.yaml does not publish
    demo compose with host networking~ins@dcompose~    image: neemle~    network_mode: host~demo/compose.yaml must not
    demo compose without tmpfs~sub@dcompose~tmpfs:~volumes:~demo/compose.yaml keeps its data in no tmpfs
    demo without its compose step~drop@demo~-f demo/compose.yaml up~~job demo-postgres has no step running
    demo without its check~drop@demo~grep -qx~~no longer checks
    no demo job~sub~  demo-postgres:~  demo-pg:~there is no job `demo-postgres`
    no test job~sub~  test:~  tests:~there is no job `test`
    """);

List<Case> StepCases() => Cases("""
    variable removed~drop~ORMLITE_TEST_POSTGRES~~does not set ORMLITE_TEST_POSTGRES
    variable blank~sub~Host=127.0.0.1;Port=55433;Username=postgres~""~non-blank
    variable at job level~ins~    timeout-minutes: 30~    env:\n      ORMLITE_TEST_POSTGRES: Host=a~belongs on the env
    variable on the demo job~ins@demo~  demo-postgres:~    env:\n      ORMLITE_TEST_POSTGRES: Host=a~belongs on the env
    no logger~sub~ --logger "trx;LogFileName=tests.trx"~~lacks `trx;LogFileName=tests.trx`
    another log file name~sub~LogFileName=tests.trx~LogFileName=other.trx~lacks `trx;LogFileName=tests.trx`
    no blame-crash~sub~ --blame-crash~~lacks `--blame-crash`
    a filter~sub~--no-build~--no-build --filter X~must not use --filter
    no --no-build~sub~--no-build~~lacks `--no-build`
    continue-on-error on the test step~ins~- name: Test~        continue-on-error: true~continue-on-error
    continue-on-error on the job~ins~    timeout-minutes: 30~    continue-on-error: true~continue-on-error
    continue-on-error on the gate~ins~--require-postgres w~        continue-on-error: true~continue-on-error
    no gate step~drop~SkipGate.cs -- check~~no step running `SkipGate.cs -- check`
    no test step~sub~dotnet test ~dotnet echo ~no step running `dotnet test`
    no shape check~drop~WorkflowShape.cs -- check~~no step running `WorkflowShape.cs -- check`
    no step-shape self-test~drop~StepShape.cs -- self-test~~no step running `StepShape.cs -- self-test`
    no step-shape check~drop~StepShape.cs -- check~~no step running `StepShape.cs -- check`
    gate before the test~before~SkipGate.cs -- check~- name: Test~must come after `dotnet test`
    build before the shape self-test~before~dotnet build~WorkflowShape.cs -- self-test~must come after
    if on the test step~ins~- name: Test~        if: always()~a step has `if: always()`
    if on the gate step~ins~--require-postgres w~        if: always()~a step has `if: always()`
    gate without the witness list~sub~ --require-postgres w~~lacks `--require-postgres`
    gate on another TRX~sub~check TestResults/tests.trx~check out/tests.trx~lacks `check TestResults
    gate without the allow-list~sub~tests/skip-allowlist.txt~x.txt~lacks `tests/skip-allowlist.txt`
    """);

List<Case> PostureCases() => Cases("""
    no timeout~drop~timeout-minutes~~no numeric timeout-minutes
    a self-hosted runner~sub~runs-on: ubuntu-latest~runs-on: self-hosted~self-hosted runner
    needs on the test job~ins~    timeout-minutes: 30~    needs: demo-postgres~must not have `needs:`
    a write permission~sub~contents: read~contents: write~is not read
    a write permission on the job~ins~    timeout-minutes: 30~    permissions:\n      id-token: write~is not read
    no permissions~cut~permissions:~env:~no top-level `permissions:`
    a secret~sub~DOTNET_NOLOGO: "1"~DOTNET_NOLOGO: ${{ secrets.X }}~references a secret
    pull_request_target~sub~  pull_request:~  pull_request_target:~`pull_request_target` is not allowed
    workflow_run~ins~  push:~  workflow_run:~`workflow_run` is not allowed
    no triggers~cut~on:~permissions:~no `on:` trigger block
    a third-party action~sub~actions/checkout@v4~other/checkout@v4~is not a first-party action
    an action on a branch~sub~actions/checkout@v4~actions/checkout@main~is not a first-party action
    """);

List<Case> ReaderCases() => Cases("""
    a TAB in the indentation~sub~    timeout-minutes: 30~\ttimeout-minutes: 30~a TAB in the indentation
    a duplicated key~ins~    timeout-minutes: 30~    timeout-minutes: 31~appears twice
    a line that is no key~ins~    timeout-minutes: 30~    not a key value~cannot be read
    keywords in a comment are ignored~ins~    timeout-minutes: 30~    # uses: evil/x@v1 secrets.X continue-on-error:~
    continue-on-error in a block scalar is no key~ins~--blame-hang-timeout 10m~              continue-on-error: true~
    a filter in a block scalar is part of the command~ins~--blame-hang-timeout 10m~              --filter X~--filter
    """);
