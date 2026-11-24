#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Run = (long Id, string Status, System.DateTimeOffset Created);

// RunRetention keeps the newest workflow runs of this repository and deletes the older ones with their logs and
// artifacts (deleting a run deletes its artifacts). It is what .github/workflows/retention.yml runs.
//   run         list the runs through `gh api`, keep the newest ones, and delete every other COMPLETED run, or
//               only list them in a dry run. Settings come from the environment: RETENTION_KEEP (default 20, at
//               least 5), RETENTION_DRY_RUN (`true` or `false`; anything else is a dry run; empty on a scheduled run,
//               which deletes only when RETENTION_APPLY is `true`), GH_TOKEN, GITHUB_REPOSITORY and GITHUB_RUN_ID
//   self-test   the selection and the setting rules, each refused case and each accepted case; no network, no gh
// What it never does: delete one of the newest runs, the run it executes in, or a run that is not completed (queued,
// in progress, waiting); and it calls the GitHub API for the list of runs and the deletion of one run only, nothing
// else (every call goes through Allowed, which refuses any other path or method), so a branch, a tag, a release, a
// ref and the contents of the repository are out of its reach by construction.
internal static class RunRetention
{
    private const int DefaultKeep = 20;
    private const int MinimumKeep = 5;
    private const string Jq = ".workflow_runs[] | [.id, .status, .created_at] | @tsv";
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

    // The ids to delete, newest first: sort by creation time (a tie by id, the higher id is the newer run), drop a run
    // listed twice, skip the newest `keep`, and of the rest take the completed ones except the current run.
    private static List<long> Doomed(IEnumerable<Run> runs, long current, int keep)
    {
        CheckKeep(keep);
        return runs.DistinctBy(r => r.Id).OrderByDescending(r => r.Created).ThenByDescending(r => r.Id).Skip(keep)
            .Where(r => r.Status == "completed" && r.Id != current).Select(r => r.Id).ToList();
    }

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

    // An explicit input decides, and only the exact text `false` deletes. A scheduled run has no input: it deletes
    // only when the repository variable RETENTION_APPLY is the exact text `true`.
    private static bool ResolveDryRun(string input, string apply) =>
        input.Length == 0 ? apply != "true" : input != "false";

    private static bool Allowed(string method, string path) =>
        (method == "GET" && ListPath.IsMatch(path)) || (method == "DELETE" && DeletePath.IsMatch(path));

    private static List<Run> ParseRuns(string tsv)
    {
        var runs = new List<Run>();
        foreach (var raw in tsv.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.TrimEnd('\r');
            var part = line.Split('\t');
            if (part.Length != 3 || !long.TryParse(part[0], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
                || !DateTimeOffset.TryParse(part[2], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                    out var created))
                throw new FormatException("cannot read the run line: " + line);
            runs.Add((id, part[1], created));
        }
        return runs;
    }

    // ---- the run: gh api and the report ----

    private static string Env(string name) => Environment.GetEnvironmentVariable(name)?.Trim() ?? "";

    private static int Execute()
    {
        var repository = Env("GITHUB_REPOSITORY");
        var runId = Env("GITHUB_RUN_ID");
        var known = long.TryParse(runId, NumberStyles.None, CultureInfo.InvariantCulture, out var current);
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
        var dry = ResolveDryRun(Env("RETENTION_DRY_RUN"), Env("RETENTION_APPLY"));
        var listed = Api("GET", "repos/" + repository + "/actions/runs?per_page=100", true);
        if (listed.Code != 0) throw new InvalidOperationException("gh api cannot list the runs: " + listed.Err.Trim());
        var runs = ParseRuns(listed.Out).DistinctBy(r => r.Id).ToList();
        var doomed = Doomed(runs, current, keep);
        Console.WriteLine($"runs: {runs.Count}, keep: {keep}, this run: {current}, dry run: {dry}");
        var byId = runs.ToDictionary(r => r.Id);
        foreach (var id in doomed)
            Console.WriteLine($"{(dry ? "would delete" : "delete")} {id} {byId[id].Status} {byId[id].Created:o}");
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

    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static Run Make(long id, string status = "completed") => (id, status, Epoch.AddMinutes(id));

    private static List<Run> Series(int count) => Enumerable.Range(1, count).Select(i => Make(i)).ToList();

    private static List<Run> With(List<Run> runs, long id, string status) =>
        runs.Select(r => r.Id == id ? Make(id, status) : r).ToList();

    private static List<Run> Tied(List<Run> runs, long id, long like) =>
        runs.Select(r => r.Id == id ? (r.Id, r.Status, runs.First(x => x.Id == like).Created) : r).ToList();

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

    private static List<string> SelectFailures()
    {
        var f = new List<string>();
        foreach (var c in SelectCases())
        {
            var got = Doomed(c.Runs, c.Current, c.Keep);
            if (!got.SequenceEqual(c.Expect))
                f.Add($"{c.Name}: expected [{string.Join(",", c.Expect)}], got [{string.Join(",", got)}]");
        }
        foreach (var keep in new[] { int.MinValue, -1, 0, 1, 4 })
            if (!Throws<ArgumentOutOfRangeException>(() => Doomed(Series(8), 99, keep)))
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

    private static List<string> SettingFailures()
    {
        var f = new List<string>();
        var dryRuns = new (string Input, string Apply, bool Dry)[]
        {
            ("", "", true), ("", "true", false), ("", "false", true), ("", "TRUE", true), ("true", "true", true),
            ("true", "", true), ("false", "", false), ("false", "true", false), ("maybe", "true", true),
        };
        foreach (var d in dryRuns.Where(d => ResolveDryRun(d.Input, d.Apply) != d.Dry))
            f.Add($"dry run for input `{d.Input}` and apply `{d.Apply}` is not {d.Dry}");
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
        var listing = "12\tcompleted\t2026-11-24T15:47:15Z\n7\tin_progress\t2026-11-24T10:00:00Z\r\n"
            + "8\t\t2026-01-01T00:00:00Z\n";
        var runs = ParseRuns(listing);
        if (runs.Count != 3 || runs[0].Id != 12 || runs[1].Status != "in_progress" || runs[2].Status != "")
            f.Add("the runs of a gh listing were not read");
        if (runs[0].Created != new DateTimeOffset(2026, 11, 24, 15, 47, 15, TimeSpan.Zero))
            f.Add("a created_at was misread");
        foreach (var bad in new[] { "x\tcompleted\t2026-01-01T00:00:00Z", "1\tcompleted", "1\tcompleted\tyesterday" })
            if (!Throws<FormatException>(() => ParseRuns(bad))) f.Add("the line `" + bad + "` was not refused");
        return f;
    }

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
        var failures = SelectFailures().Concat(SettingFailures()).Concat(InputFailures()).Concat(PathFailures())
            .ToList();
        foreach (var message in failures) Console.Error.WriteLine("SELF-TEST FAIL: " + message);
        var count = SelectCases().Count;
        Console.WriteLine($"self-test: {count} selection cases and the other rules, {failures.Count} failed");
        return failures.Count == 0 ? 0 : 1;
    }
}
