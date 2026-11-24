#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Run = (long Id, string Status, System.DateTimeOffset Created, long Workflow);

// RunRetention keeps the newest workflow runs of this repository and deletes the older ones with their logs and
// artifacts (deleting a run deletes its artifacts). It is what .github/workflows/retention.yml runs, after every
// finished ci run, daily, and on request; it deletes by default and has no gate.
//   run         list the runs through `gh api`, keep the newest ones, and delete every other COMPLETED run, or only
//               list them in a dry run. Settings come from the environment: RETENTION_KEEP (default 20, at least 5;
//               empty on a run that is not a manual one), RETENTION_DRY_RUN (`true` lists only; empty or `false`
//               deletes, and empty is what a run that is not a manual one has; any other text is refused), GH_TOKEN,
//               GITHUB_REPOSITORY and GITHUB_RUN_ID
//   self-test   the selection and the setting rules, each refused case and each accepted case; no network, no gh
// The newest `keep` are counted over ALL workflow runs of the repository together (ci and every other workflow), except
// the runs of the retention workflow itself, which is the workflow of the run this script executes in. Those are left
// out of that count, so the one run per finished ci run can never push a ci run out of the newest 20, and they are
// trimmed on their own to the newest OwnKeep (5), so their history does not grow.
// What it never does: delete one of the newest runs, the run it executes in, or a run that is not completed (queued,
// in progress, waiting); and it calls the GitHub API for the list of runs and the deletion of one run only, nothing
// else (every call goes through Allowed, which refuses any other path or method), so a branch, a tag, a release, a
// ref and the contents of the repository are out of its reach by construction. A run that is already gone (HTTP 404)
// counts as deleted. When the run it executes in is not in the listing it refuses, as it cannot tell its own runs.
internal static class RunRetention
{
    private const int DefaultKeep = 20;
    private const int MinimumKeep = 5;
    private const int OwnKeep = 5;
    private const string Jq = ".workflow_runs[] | [.id, .status, .created_at, .workflow_id] | @tsv";
    private const string Runs = @"repos/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/actions/runs";

    private static readonly Regex Slug = new(@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+\z", RegexOptions.CultureInvariant);
    private static readonly Regex ListPath = new("^" + Runs + @"\?per_page=100\z", RegexOptions.CultureInvariant);
    private static readonly Regex DeletePath = new("^" + Runs + @"/[0-9]+\z", RegexOptions.CultureInvariant);

    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "self-test") return SelfTest();
        if (args.Length == 1 && args[0] == "run") return Execute();
        Console.Error.WriteLine("usage: RunRetention.cs run | self-test");
        return 2;
    }

    // ---- the rules, pure ----

    // The ids to delete: drop a run listed twice, keep the newest `keep` of the runs of every workflow but the
    // retention workflow `own` (together, not per workflow), keep the newest OwnKeep of the retention workflow, and
    // delete what is beyond them.
    private static List<long> Doomed(IEnumerable<Run> runs, long own, long current, int keep)
    {
        CheckKeep(keep);
        var all = runs.DistinctBy(r => r.Id).ToList();
        return Beyond(all.Where(r => r.Workflow != own), current, keep)
            .Concat(Beyond(all.Where(r => r.Workflow == own), current, OwnKeep)).ToList();
    }

    // The workflow of the run this script executes in, the retention workflow; no answer when that run is not listed.
    private static long OwnWorkflow(IEnumerable<Run> runs, long current)
    {
        var mine = runs.Where(r => r.Id == current).ToList();
        if (mine.Count == 0) throw new InvalidOperationException("run " + current + " is not in the listing of runs");
        return mine[0].Workflow;
    }

    // The ids of some runs, newest first: sort by creation time (a tie by id, the higher id is the newer run), skip the
    // newest `keep`, and of the rest take the completed ones except the current run.
    private static IEnumerable<long> Beyond(IEnumerable<Run> runs, long current, int keep) =>
        runs.OrderByDescending(r => r.Created).ThenByDescending(r => r.Id).Skip(keep)
            .Where(r => r.Status == "completed" && r.Id != current).Select(r => r.Id);

    private static int CheckKeep(int keep)
    {
        if (keep < MinimumKeep)
            throw new ArgumentOutOfRangeException(nameof(keep), $"keep {keep} is below the minimum {MinimumKeep}");
        return keep;
    }

    // An empty text is the default; a whole number is taken ("20.0" too, which a number input may print).
    private static int ParseKeep(string text)
    {
        if (text.Length == 0) return DefaultKeep;
        var ok = decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number);
        if (!ok || number != decimal.Floor(number) || number > 100000)
            throw new ArgumentException("keep `" + text + "` is not a whole number");
        return CheckKeep((int)number);
    }

    // Only the exact text `true` is a dry run. An empty text (a run that is not a manual one has no input) and the
    // text `false` delete; any other text is refused, so a typo is neither a deletion nor a silent dry run.
    private static bool ParseDryRun(string text) => text switch
    {
        "" or "false" => false,
        "true" => true,
        _ => throw new ArgumentException("dry_run `" + text + "` is neither true nor false"),
    };

    private static bool Allowed(string method, string path) =>
        (method == "GET" && ListPath.IsMatch(path)) || (method == "DELETE" && DeletePath.IsMatch(path));

    private static List<Run> ParseRuns(string tsv)
    {
        var runs = new List<Run>();
        foreach (var raw in tsv.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.TrimEnd('\r');
            var part = line.Split('\t');
            if (part.Length != 4 || !Number(part[0], out var id) || !Number(part[3], out var workflow)
                || !DateTimeOffset.TryParse(part[2], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                    out var created))
                throw new FormatException("cannot read the run line: " + line);
            runs.Add((id, part[1], created, workflow));
        }
        return runs;
    }

    private static bool Number(string text, out long number) =>
        long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number);

    // ---- the run: gh api and the report ----

    private static string Env(string name) => Environment.GetEnvironmentVariable(name)?.Trim() ?? "";

    private static int Execute()
    {
        var repository = Env("GITHUB_REPOSITORY");
        var runId = Env("GITHUB_RUN_ID");
        var known = Number(runId, out var current);
        if (!Slug.IsMatch(repository) || !known)
        {
            Console.Error.WriteLine("FAIL: GITHUB_REPOSITORY (owner/name) and GITHUB_RUN_ID (a number) must be set");
            return 2;
        }
        try
        {
            return Apply(repository, current);
        }
        catch (Exception e) when (e is ArgumentException or FormatException or InvalidOperationException)
        {
            Console.Error.WriteLine("FAIL: " + e.Message);
            return 1;
        }
    }

    private static int Apply(string repository, long current)
    {
        var keep = ParseKeep(Env("RETENTION_KEEP"));
        var dry = ParseDryRun(Env("RETENTION_DRY_RUN"));
        var listed = Api("GET", "repos/" + repository + "/actions/runs?per_page=100", true);
        if (listed.Code != 0) throw new InvalidOperationException("gh api cannot list the runs: " + listed.Err.Trim());
        var runs = ParseRuns(listed.Out).DistinctBy(r => r.Id).ToList();
        var doomed = Doomed(runs, OwnWorkflow(runs, current), current, keep);
        Console.WriteLine($"runs: {runs.Count}, keep: {keep}, this run: {current}, dry run: {dry}");
        var byId = runs.ToDictionary(r => r.Id);
        foreach (var id in doomed)
            Console.WriteLine($"{(dry ? "would delete" : "delete")} {id} {byId[id].Status} {byId[id].Created:o} "
                + $"workflow {byId[id].Workflow}");
        Console.WriteLine((dry ? "would delete " : "deleting ") + doomed.Count + " of " + runs.Count + " runs");
        if (dry) return 0;
        var failed = doomed.Count(id => !Delete(repository, id));
        return failed == 0 ? 0 : 1;
    }

    private static bool Delete(string repository, long id)
    {
        var result = Api("DELETE", "repos/" + repository + "/actions/runs/" + id, false);
        if (result.Code == 0 || result.Err.Contains("HTTP 404", StringComparison.Ordinal)) return true;
        Console.Error.WriteLine("FAIL: run " + id + ": " + result.Err.Trim());
        return false;
    }

    private static (int Code, string Out, string Err) Api(string method, string path, bool paginate)
    {
        if (!Allowed(method, path)) throw new InvalidOperationException("refused: " + method + " " + path);
        var start = new ProcessStartInfo("gh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in new[] { "api", "--method", method }) start.ArgumentList.Add(arg);
        if (paginate)
            foreach (var arg in new[] { "--paginate", "--jq", Jq }) start.ArgumentList.Add(arg);
        start.ArgumentList.Add(path);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("gh did not start");
        var err = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output, err.Result);
    }

    // ---- self-test ----

    private const long Own = 9;

    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Run Make(long id, string status = "completed", long workflow = 1) =>
        (id, status, Epoch.AddMinutes(id), workflow);

    private static List<Run> Series(int count) => Enumerable.Range(1, count).Select(i => Make(i)).ToList();

    private static List<Run> With(List<Run> runs, long id, string status) =>
        runs.Select(r => r.Id == id ? Make(id, status, r.Workflow) : r).ToList();

    private static List<Run> Tied(List<Run> runs, long id, long like) =>
        runs.Select(r => r.Id == id ? (r.Id, r.Status, runs.First(x => x.Id == like).Created, r.Workflow) : r)
            .ToList();

    private static List<(string Name, List<Run> Runs, long Current, int Keep, long[] Expect)> SelectCases()
    {
        var eight = Series(8);
        var scrambled = eight.OrderBy(r => r.Id * 5 % 8).ToList();
        var waiting = With(With(eight, 3, "in_progress"), 1, "queued");
        return
        [
            ("fewer runs than keep", Series(3), 99, 5, Array.Empty<long>()),
            ("exactly keep", Series(5), 99, 5, Array.Empty<long>()),
            ("one more than keep", Series(6), 99, 5, new long[] { 1 }),
            ("more than keep", eight, 99, 5, new long[] { 3, 2, 1 }),
            ("no runs", new List<Run>(), 99, 5, Array.Empty<long>()),
            ("unsorted input", scrambled, 99, 5, new long[] { 3, 2, 1 }),
            ("the current run among the old ones", eight, 2, 5, new long[] { 3, 1 }),
            ("the current run among the newest", eight, 8, 5, new long[] { 3, 2, 1 }),
            ("old runs that are not completed", waiting, 99, 5, new long[] { 2 }),
            ("a waiting run among the newest", With(eight, 8, "waiting"), 99, 5, new long[] { 3, 2, 1 }),
            ("ties in created_at", Tied(Series(7), 3, 2), 99, 5, new long[] { 2, 1 }),
            ("ties in created_at, other order", Enumerable.Reverse(Tied(Series(7), 3, 2)).ToList(), 99, 5,
                new long[] { 2, 1 }),
            ("a run listed twice", Series(7).Append(Make(7)).ToList(), 99, 5, new long[] { 2, 1 }),
            ("a keep above the count", eight, 99, 50, Array.Empty<long>()),
            ("a larger keep", eight, 99, 7, new long[] { 1 }),
        ];
    }

    // ids 1 to `ci` of the ci workflow (1), then ids 21 to 20 + `own` of the retention workflow (9), newer than them
    private static List<Run> Mixed(int ci, int own) => Enumerable.Range(1, ci).Select(i => Make(i))
        .Concat(Enumerable.Range(21, own).Select(i => Make(i, workflow: Own))).ToList();

    private static long[] Plus(long[] own, params long[] ci) => ci.Concat(own).ToArray();

    private static List<(string Name, List<Run> Runs, long Current, int Keep, long[] Expect)> WorkflowCases()
    {
        var alternate = Enumerable.Range(1, 12).Select(i => Make(i, workflow: i % 2 + 1)).ToList();
        var own = new long[] { 25, 24, 23, 22, 21 };
        return
        [
            ("every workflow counts in one newest list", alternate, 99, 5, new long[] { 7, 6, 5, 4, 3, 2, 1 }),
            ("the retention runs are left out of the count", Mixed(8, 10), 99, 5, Plus(own, 3, 2, 1)),
            ("ci runs fewer than keep, retention runs many", Mixed(5, 10), 99, 5, own),
            ("the retention runs keep five whatever keep is", Mixed(8, 10), 99, 7, Plus(own, 1)),
            ("fewer than five retention runs", Mixed(8, 4), 99, 5, new long[] { 3, 2, 1 }),
            ("the running retention run", Mixed(8, 10), 24, 5, new long[] { 3, 2, 1, 25, 23, 22, 21 }),
            ("a retention run that is not completed", With(Mixed(8, 10), 22, "queued"), 99, 5,
                new long[] { 3, 2, 1, 25, 24, 23, 21 }),
            ("a ci run that is not completed", With(Mixed(8, 10), 2, "in_progress"), 99, 5,
                new long[] { 3, 1, 25, 24, 23, 22, 21 }),
        ];
    }

    private static List<string> SelectFailures()
    {
        var f = new List<string>();
        foreach (var c in SelectCases().Concat(WorkflowCases()))
        {
            var got = Doomed(c.Runs, Own, c.Current, c.Keep);
            if (!got.SequenceEqual(c.Expect))
                f.Add($"{c.Name}: expected [{string.Join(",", c.Expect)}], got [{string.Join(",", got)}]");
        }
        if (OwnWorkflow(Mixed(3, 3), 22) != Own || !Throws<InvalidOperationException>(() => OwnWorkflow(Series(3), 99)))
            f.Add("the retention workflow was not read from the current run, or an unlisted run was not refused");
        foreach (var keep in new[] { int.MinValue, -1, 0, 1, 4 })
            if (!Throws<ArgumentOutOfRangeException>(() => Doomed(Series(8), Own, 99, keep)))
                f.Add("keep " + keep + " is below the minimum and was not refused");
        return f;
    }

    private static bool Throws<T>(Action action) where T : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (Exception e) when (e is T)
        {
            return true;
        }
    }

    // Only `true` is a dry run; an empty text and `false` delete; anything else is refused.
    private static List<string> DryRunFailures()
    {
        var f = new List<string>();
        var accepted = new (string Text, bool Dry)[] { ("", false), ("false", false), ("true", true) };
        foreach (var a in accepted.Where(a => ParseDryRun(a.Text) != a.Dry))
            f.Add($"dry run for `{a.Text}` is not {a.Dry}");
        foreach (var text in new[] { "TRUE", "False", "maybe", "1", "0", "yes", "no", " ", "true ", "null" })
            if (!Throws<ArgumentException>(() => ParseDryRun(text))) f.Add("dry run `" + text + "` was not refused");
        return f;
    }

    private static List<string> SettingFailures()
    {
        var f = new List<string>();
        var keeps = new (string Text, int Keep)[] { ("", 20), ("20", 20), ("20.0", 20), ("5", 5), ("100", 100) };
        foreach (var k in keeps.Where(k => ParseKeep(k.Text) != k.Keep))
            f.Add("keep `" + k.Text + "` is not " + k.Keep);
        foreach (var text in new[] { "4", "0", "-3", "abc", "7.5", "1e3", "999999999999" })
            if (!Throws<ArgumentException>(() => ParseKeep(text))) f.Add("keep `" + text + "` was not refused");
        return f;
    }

    private static List<string> InputFailures()
    {
        var f = new List<string>();
        var listing = "12\tcompleted\t2026-11-24T15:47:15Z\t5\n7\tin_progress\t2026-11-24T10:00:00Z\t6\r\n"
            + "8\t\t2026-01-01T00:00:00Z\t6\n";
        var runs = ParseRuns(listing);
        if (runs.Count != 3 || runs[0].Id != 12 || runs[1].Status != "in_progress" || runs[2].Status != "")
            f.Add("the runs of a gh listing were not read");
        if (runs[0].Created != new DateTimeOffset(2026, 11, 24, 15, 47, 15, TimeSpan.Zero))
            f.Add("a created_at was misread");
        if (runs[0].Workflow != 5 || runs[1].Workflow != 6) f.Add("a workflow_id was misread");
        foreach (var bad in BadLines())
            if (!Throws<FormatException>(() => ParseRuns(bad))) f.Add("the line `" + bad + "` was not refused");
        return f;
    }

    private static string[] BadLines() =>
    [
        "x\tcompleted\t2026-01-01T00:00:00Z\t5", "1\tcompleted\t2026-01-01T00:00:00Z", "1\tcompleted",
        "1\tcompleted\tyesterday\t5", "1\tcompleted\t2026-01-01T00:00:00Z\tw", "1\tcompleted\t2026-01-01T00:00:00Z\t-3",
    ];

    private static List<string> PathFailures()
    {
        var f = new List<string>();
        const string Base = "repos/neemle/orm.lite/";
        var allowed = new (string Method, string Path)[]
        {
            ("GET", Base + "actions/runs?per_page=100"), ("DELETE", Base + "actions/runs/12345"),
        };
        foreach (var a in allowed.Where(a => !Allowed(a.Method, a.Path)))
            f.Add("the call " + a.Method + " " + a.Path + " was refused");
        var refused = new (string Method, string Path)[]
        {
            ("DELETE", Base + "actions/runs?per_page=100"), ("GET", Base + "actions/runs/12345"),
            ("DELETE", Base + "actions/runs/abc"), ("DELETE", Base + "actions/runs/1/artifacts"),
            ("DELETE", Base + "actions/artifacts/9"), ("DELETE", Base + "git/refs/heads/main"),
            ("DELETE", Base + "releases/1"), ("DELETE", Base + "branches/main"),
            ("POST", Base + "actions/runs/1/rerun"),
            ("GET", Base + "actions/runs"), ("PUT", Base + "actions/runs/1"), ("DELETE", "user/repos/x/actions/runs/1"),
        };
        foreach (var r in refused.Where(r => Allowed(r.Method, r.Path)))
            f.Add("the call " + r.Method + " " + r.Path + " was allowed");
        if (Slug.IsMatch("a/b/c") || Slug.IsMatch("a") || !Slug.IsMatch("neemle/orm.lite"))
            f.Add("the repository name rule is wrong");
        return f;
    }

    private static int SelfTest()
    {
        var failures = SelectFailures().Concat(DryRunFailures()).Concat(SettingFailures()).Concat(InputFailures())
            .Concat(PathFailures()).ToList();
        foreach (var message in failures) Console.Error.WriteLine("SELF-TEST FAIL: " + message);
        var count = SelectCases().Count + WorkflowCases().Count;
        Console.WriteLine($"self-test: {count} selection cases and the other rules, {failures.Count} failed");
        return failures.Count == 0 ? 0 : 1;
    }
}
