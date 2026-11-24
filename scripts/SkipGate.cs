#:property EnableAotAnalyzer=false
#:property EnableTrimAnalyzer=false
#:property PublishAot=false
// Skip gate for the test job (B54). A skipped xunit test is not a failure: with no PostgreSQL service the test host
// exits 0 over hundreds of skipped server tests. This gate reads the TRX the test host wrote and fails unless every
// skipped test is on a reasoned allow-list, and (with --require-postgres) every server-only witness class has a Passed
// result. It prints names, outcomes and reasons only; the TRX also carries user and machine names, never echoed.
//   dotnet run scripts/SkipGate.cs -- check <trx> <allow-list> [--require-postgres <witness-list>]
//                                                     exit 0 pass, 1 gate failure, 2 usage
//   dotnet run scripts/SkipGate.cs -- self-test       no server and no TRX needed; every rule has a negative case
// Allow-list (tests/skip-allowlist.txt): `<class>.<test method>` TAB `<reason>`, `#` comments and blank lines allowed.
// Witness list (tests/skip-witness.txt):  `<class>` TAB `<reason>`; hand-written, never captured from a run.
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Case = (string Name, string Target, string Find, string Replace, string Expect);
using Row = (string Class, string Test, string Outcome);

if (args.Length == 1 && args[0] == "self-test") return SelfTest();
if (args.Length is 3 or 5 && args[0] == "check" && (args.Length == 3 || args[3] == "--require-postgres"))
    return Check(args[1], args[2], args.Length == 5 ? args[4] : null);
Console.Error.WriteLine("usage: SkipGate.cs check <trx> <allow-list> [--require-postgres <witness-list>] | self-test");
return 2;

int Check(string trx, string allow, string? witness)
{
    var (fails, notes) = Judge(trx, allow, witness);
    foreach (var message in fails) Console.Error.WriteLine("FAIL: " + message);
    if (fails.Count == 0) foreach (var note in notes) Console.WriteLine(note);
    return fails.Count == 0 ? 0 : 1;
}

(List<string> Fails, List<string> Notes) Judge(string trx, string allow, string? witness)
{
    var f = new List<string>();
    var rows = ReadTrx(trx, f);
    var listed = ReadList(allow, "allow-list", f);
    var witnesses = witness is null ? null : ReadList(witness, "witness list", f);
    var skipped = JudgeRows(rows, listed, f);
    foreach (var key in listed.Keys.Where(key => !skipped.Contains(key)))
        f.Add("stale allow-list entry (the test did not skip): " + key);
    JudgeWitnesses(rows, witnesses, f);
    return (f, Notes(rows, listed, witnesses?.Count ?? 0));
}

List<string> Notes(List<Row> rows, Dictionary<string, string> listed, int witnesses)
{
    var notes = new List<string>();
    foreach (var g in rows.GroupBy(r => r.Class).OrderBy(g => g.Key, StringComparer.Ordinal))
        notes.Add($"{g.Key}: passed {g.Count(r => r.Outcome == "Passed")}, "
            + $"skipped {g.Count(r => r.Outcome == "NotExecuted")}, "
            + $"other {g.Count(r => r.Outcome is not ("Passed" or "NotExecuted"))}");
    foreach (var (key, reason) in listed) notes.Add("allow-listed skip: " + key + " (" + reason + ")");
    notes.Add($"PASS: {rows.Count} results, every skip is allow-listed, {witnesses} witness classes passed.");
    return notes;
}

// ---- the TRX: identity is TestMethod className + name, joined to each result by testId ----

List<Row> ReadTrx(string path, List<string> f)
{
    var rows = new List<Row>();
    if (!File.Exists(path))
    {
        f.Add("the TRX does not exist: " + path);
        return rows;
    }
    XElement? root;
    try
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(path, settings);
        root = XDocument.Load(reader).Root;
    }
    catch (Exception e) when (e is XmlException or IOException or UnauthorizedAccessException)
    {
        f.Add("the TRX cannot be read as XML (empty, torn, malformed or with a DOCTYPE)");
        return rows;
    }
    if (root is null || root.Name.LocalName != "TestRun") f.Add("the TRX root is not a TestRun");
    else CollectRows(root, rows, f);
    return rows;
}

Dictionary<string, (string Class, string Name)> Identities(XElement root)
{
    var identity = new Dictionary<string, (string Class, string Name)>(StringComparer.Ordinal);
    foreach (var unit in Named(root, "UnitTest"))
    {
        var method = Named(unit, "TestMethod").FirstOrDefault();
        var id = (string?)unit.Attribute("id");
        if (method is not null && id is not null)
            identity[id] = ((string?)method.Attribute("className") ?? "", (string?)method.Attribute("name") ?? "");
    }
    return identity;
}

void CollectRows(XElement root, List<Row> rows, List<string> f)
{
    var identity = Identities(root);
    var results = Named(root, "UnitTestResult").ToList();
    foreach (var result in results)
    {
        var id = (string?)result.Attribute("testId");
        if (id is not null && identity.TryGetValue(id, out var who))
            rows.Add((who.Class, who.Name, (string?)result.Attribute("outcome") ?? ""));
        else f.Add("a result has no test definition: " + Clip((string?)result.Attribute("testName")));
    }
    if (results.Count == 0) f.Add("the TRX holds no test result");
    CheckSummary(root, results.Count, f);
}

void CheckSummary(XElement root, int results, List<string> f)
{
    var summary = Named(root, "ResultSummary").FirstOrDefault();
    var outcome = (string?)summary?.Attribute("outcome");
    var total = (string?)Named(summary ?? root, "Counters").FirstOrDefault()?.Attribute("total");
    if (summary is null) f.Add("the run summary is missing");
    else if (outcome != "Completed") f.Add("the run summary outcome is " + Clip(outcome) + ", not Completed");
    if (summary is not null && total is null) f.Add("the run summary has no Counters total");
    else if (total is not null && total != results.ToString(System.Globalization.CultureInfo.InvariantCulture))
        f.Add("the Counters total is " + Clip(total) + " but the TRX holds " + results + " results");
}

IEnumerable<XElement> Named(XElement parent, string name) => parent.Descendants().Where(e => e.Name.LocalName == name);

string Clip(string? text) => text is null ? "(none)" : text.Length > 40 ? text[..40] : text;

HashSet<string> JudgeRows(List<Row> rows, Dictionary<string, string> listed, List<string> f)
{
    var seen = new HashSet<string>(StringComparer.Ordinal);
    var problems = new Dictionary<string, int>(StringComparer.Ordinal);
    foreach (var (cls, test, outcome) in rows.Where(r => r.Outcome != "Passed"))
    {
        var key = cls + "." + test;
        var skip = "skipped and not on the allow-list: " + key;
        var problem = outcome == "NotExecuted" ? skip : key + " ended " + Clip(outcome);
        if (outcome == "NotExecuted" && listed.ContainsKey(key)) seen.Add(key);
        else problems[problem] = problems.GetValueOrDefault(problem) + 1;
    }
    f.AddRange(problems.Select(p => p.Value > 1 ? p.Key + " (" + p.Value + " results)" : p.Key));
    return seen;
}

void JudgeWitnesses(List<Row> rows, Dictionary<string, string>? witnesses, List<string> f)
{
    if (witnesses is null) return;
    if (witnesses.Count == 0) f.Add("the witness list is empty");
    foreach (var cls in witnesses.Keys)
        if (!rows.Any(r => r.Class == cls)) f.Add("witness class " + cls + " is absent from the TRX");
        else if (!rows.Any(r => r.Class == cls && r.Outcome == "Passed"))
            f.Add("witness class " + cls + " has no Passed result");
}

// ---- the two lists: key TAB reason; one stated rule refuses CR, a BOM and anything but printable ASCII in a key ----

string? Decode(string path, string label, List<string> f)
{
    if (!File.Exists(path))
    {
        f.Add("the " + label + " does not exist: " + path);
        return null;
    }
    var bytes = File.ReadAllBytes(path);
    if (bytes.AsSpan().StartsWith<byte>([0xEF, 0xBB, 0xBF])) f.Add("the " + label + " starts with a byte order mark");
    try
    {
        return new UTF8Encoding(false, true).GetString(bytes);
    }
    catch (DecoderFallbackException)
    {
        f.Add("the " + label + " is not valid UTF-8");
        return null;
    }
}

Dictionary<string, string> ReadList(string path, string label, List<string> f)
{
    var map = new Dictionary<string, string>(StringComparer.Ordinal);
    var lines = (Decode(path, label, f) ?? "").Split('\n');
    for (var i = 0; i < lines.Length; i++)
        if (lines[i].Length > 0 && lines[i][0] != '#') AddEntry(map, label + " line " + (i + 1), lines[i], f);
    return map;
}

string? Problem(string line, int tab, string key, string reason)
{
    if (line.Contains('\r')) return "carries a carriage return (CRLF is refused)";
    if (tab < 0) return "has no TAB between the key and the reason";
    if (key.Length == 0 || key.Any(c => c < '!' || c > '~')) return "has a key outside printable ASCII";
    return reason.Length == 0 ? "has no reason" : null;
}

void AddEntry(Dictionary<string, string> map, string where, string line, List<string> f)
{
    var tab = line.IndexOf('\t');
    var key = tab < 0 ? line : line[..tab];
    var reason = tab < 0 ? "" : line[(tab + 1)..].Trim();
    var error = Problem(line, tab, key, reason);
    if (error is not null) f.Add(where + ": " + error);
    else if (!map.TryAdd(key, reason)) f.Add(where + ": duplicates the entry for " + key);
}

// ---- self-test: hand-written fixtures through a small TRX writer; one case per rule ----

string BaseRows() => """
PostgresBehaviourTests|One|Passed
PostgresBehaviourTests|Two|Passed
PostgresMigrationSetTests|Set|Passed
PostgresIndexTests|Collation|NotExecuted
PostgresIndexTests|Other|Passed
PostgresSkipOnlyTests|Only|NotExecuted
SqliteTests|S|Passed
""";

string BaseAllow() => "# a comment\n\nN.PostgresIndexTests.Collation\tPostgreSQL has no such refusal\n"
    + "N.PostgresSkipOnlyTests.Only\tskipped on every provider\n";

string BaseWitness() => "N.PostgresBehaviourTests\tserver only\nN.PostgresMigrationSetTests\tserver only\n";

string Trx(string rowsSpec)
{
    var results = new StringBuilder();
    var definitions = new StringBuilder();
    var lines = rowsSpec.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    for (var i = 0; i < lines.Length; i++)
    {
        var p = lines[i].Split('|');
        results.Append($"<UnitTestResult testId=\"t{i}\" testName=\"{p[1]}\" outcome=\"{p[2]}\" ")
            .Append("computerName=\"SECRET-HOST\" />");
        definitions.Append($"<UnitTest id=\"t{i}\" name=\"x\">")
            .Append($"<TestMethod className=\"N.{p[0]}\" name=\"{p[1]}\" /></UnitTest>");
    }
    return "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
        + "<TestRun xmlns=\"http://microsoft.com/schemas/VisualStudio/TeamTest/2010\">"
        + $"<Results>{results}</Results><TestDefinitions>{definitions}</TestDefinitions>"
        + $"<ResultSummary outcome=\"Completed\"><Counters total=\"{lines.Length}\" /></ResultSummary></TestRun>";
}

// a case edits one artifact (rows, allow, witness or xml): Find "" keeps it, "*" replaces all of it, else Find becomes
// Replace; the Replace "<none>" writes no file and, for the witness, "<flag-off>" omits --require-postgres
string? Edit(string target, string text, Case c)
{
    if (c.Target != target || c.Find.Length == 0) return text;
    if (c.Find == "*") return c.Replace;
    if (!text.Contains(c.Find, StringComparison.Ordinal)) return null;
    return text.Replace(c.Find, c.Replace, StringComparison.Ordinal);
}

string?[] Artifacts(Case c)
{
    var rows = Edit("rows", BaseRows(), c);
    var xml = rows is null ? null : Edit("xml", Trx(rows), c);
    return [xml, Edit("allow", BaseAllow(), c), Edit("witness", BaseWitness(), c)];
}

bool Holds(Case c, string dir)
{
    var parts = Artifacts(c);
    var paths = new[] { "t.trx", "allow.txt", "witness.txt" }.Select(name => Path.Combine(dir, name)).ToArray();
    if (parts.Any(p => p is null)) return Report(c, ["the edit does not apply to the fixture"], false);
    for (var i = 0; i < 3; i++)
        if (parts[i] is "<none>" or "<flag-off>") paths[i] += ".absent";
        else File.WriteAllText(paths[i], parts[i]!, new UTF8Encoding(false));
    var (fails, _) = Judge(paths[0], paths[1], parts[2] == "<flag-off>" ? null : paths[2]);
    return Report(c, fails, true);
}

bool Report(Case c, List<string> fails, bool applied)
{
    var found = fails.Any(m => m.Contains(c.Expect, StringComparison.Ordinal));
    var ok = applied && (c.Expect.Length == 0 ? fails.Count == 0 : found);
    if (fails.Any(m => m.Contains("SECRET-HOST", StringComparison.Ordinal))) ok = false;
    if (!ok)
        Console.Error.WriteLine($"SELF-TEST FAIL: {c.Name}: expected `{c.Expect}`, got: "
            + (fails.Count == 0 ? "none" : string.Join(" | ", fails)));
    return ok;
}

int SelfTest()
{
    var dir = Path.Combine(Path.GetTempPath(), "orm-lite-skipgate-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    try
    {
        var cases = TrxCases().Concat(OutcomeCases()).Concat(ListCases()).Concat(WitnessCases()).ToList();
        var failed = cases.Count(c => !Holds(c, dir));
        Console.WriteLine("self-test: " + cases.Count + " cases, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }
    finally
    {
        Directory.Delete(dir, true);
    }
}

// a case is name ~ artifact ~ find ~ replace ~ expected failure (empty: the gate must pass); the escapes
// \t, \n, \r, \uFEFF and \u0456 stand for the characters they name
List<Case> Cases(string table) => table.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split('~'))
    .Select(p => (p[0], p[1], Unescape(p[2]), Unescape(p[3]), p[4])).ToList();

string Unescape(string text) => text.Replace("\\t", "\t").Replace("\\n", "\n").Replace("\\r", "\r")
    .Replace("\\uFEFF", "\uFEFF").Replace("\\u0456", "\u0456");

List<Case> TrxCases() => Cases("""
    control: all passed plus the allow-listed skips~rows~~~
    an unlisted skip~rows~SqliteTests|S|Passed~SqliteTests|S|NotExecuted~not on the allow-list: N.SqliteTests.S
    a stale entry, the listed test passed~rows~|Collation|NotExecuted~|Collation|Passed~stale allow-list entry
    a stale entry, the listed test was renamed~rows~|Collation|~|Collation2|~stale allow-list entry
    a missing TRX~xml~*~<none>~the TRX does not exist
    an empty TRX file~xml~*~~the TRX cannot be read as XML
    a torn TRX~xml~</TestRun>~~the TRX cannot be read as XML
    a TRX with a DOCTYPE~xml~<TestRun~<!DOCTYPE TestRun [<!ENTITY a "b">]><TestRun~the TRX cannot be read as XML
    a TRX with no result~rows~*~~the TRX holds no test result
    a TRX that is not a TestRun~xml~TestRun~Other~the TRX root is not a TestRun
    Counters total above the rows~xml~total="7"~total="8"~the Counters total is 8 but the TRX holds 7 results
    Counters total below the rows~xml~total="7"~total="6"~the Counters total is 6
    no Counters~xml~<Counters~<Counterz~the run summary has no Counters total
    a run summary that is not Completed~xml~ResultSummary outcome="Completed"~ResultSummary outcome="Aborted"~is Aborted
    no run summary~xml~ResultSummary~ResultSummaryX~the run summary is missing
    a result without a definition~xml~<UnitTest id="t0"~<UnitTest id="zz"~has no test definition
    """);

List<Case> OutcomeCases() => Cases("""
    a Failed result~rows~SqliteTests|S|Passed~SqliteTests|S|Failed~N.SqliteTests.S ended Failed
    an Error result~rows~SqliteTests|S|Passed~SqliteTests|S|Error~N.SqliteTests.S ended Error
    a Timeout result~rows~SqliteTests|S|Passed~SqliteTests|S|Timeout~N.SqliteTests.S ended Timeout
    an Aborted result~rows~SqliteTests|S|Passed~SqliteTests|S|Aborted~N.SqliteTests.S ended Aborted
    an Inconclusive result~rows~SqliteTests|S|Passed~SqliteTests|S|Inconclusive~N.SqliteTests.S ended Inconclusive
    an unknown outcome~rows~SqliteTests|S|Passed~SqliteTests|S|Exploded~N.SqliteTests.S ended Exploded
    an empty outcome~rows~SqliteTests|S|Passed~SqliteTests|S|~N.SqliteTests.S ended
    Theory rows, one entry covers mixed outcomes~rows~|Collation|NotExecuted~|Collation|NotExecuted\n|Collation|Passed~
    """);

List<Case> ListCases() => Cases("""
    an allow-list entry without a reason~allow~\tPostgreSQL has no such refusal~\t~allow-list line 3: has no reason
    an allow-list line without a TAB~allow~\tPostgreSQL has no~ PostgreSQL has no~allow-list line 3: has no TAB
    a duplicate allow-list entry~allow~refusal\n~refusal\nN.PostgresIndexTests.Collation\tagain\n~duplicates
    a missing allow-list~allow~*~<none>~the allow-list does not exist
    a prefix-colliding allow-list entry~allow~Collation\t~CollationX\t~not on the allow-list: N.PostgresIndexTests.Coll
    CRLF in the allow-list~allow~refusal\n~refusal\r\n~carriage return
    a byte order mark in the allow-list~allow~# a comment~\uFEFF# a comment~byte order mark
    a non-ASCII lookalike in an entry~allow~Collation\t~Collat\u0456on\t~outside printable ASCII
    a trailing space in an entry key~allow~Collation\t~Collation \t~outside printable ASCII
    comments and blank lines are fine~allow~# a comment~# one\n\n# two~
    """);

List<Case> WitnessCases() => Cases("""
    a witness class is absent~rows~PostgresMigrationSetTests|Set|Passed~SqliteTests|T|Passed~MigrationSetTests is absent
    a witness with only skipped results~witness~server only\n~server only\nN.PostgresSkipOnlyTests\tx\n~has no Passed
    a witness line has no reason~witness~BehaviourTests\tserver only~BehaviourTests\t~witness list line 1: has no reason
    a duplicate witness line~witness~server only\n~server only\nN.PostgresBehaviourTests\tagain\n~duplicates
    a missing witness list~witness~*~<none>~the witness list does not exist
    an empty witness list~witness~*~~the witness list is empty
    a witness class is a prefix of a real one~witness~BehaviourTests\t~BehaviourTest\t~PostgresBehaviourTest is absent
    a witness class with a longer name~witness~BehaviourTests\t~BehaviourTestsX\t~PostgresBehaviourTestsX is absent
    a witness line without a TAB~witness~SetTests\tserver~SetTests server~witness list line 2: has no TAB
    no witness flag, the witnesses are not judged~witness~*~<flag-off>~
    """);
