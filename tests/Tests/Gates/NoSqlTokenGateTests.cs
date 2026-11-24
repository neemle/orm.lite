using Xunit;

namespace Neemle.OrmLite.Tests.Gates;

public sealed class NoSqlTokenGateTests
{
    private const string Baseline = "Gates/legacy-sql-literals.txt";

    private static readonly string[] Scanned =
    [
        "src/Runtime",
        "src/Generator",
    ];

    private static Dictionary<string, int> Counts()
    {
        var root = RuntimeSources.LibraryRoot();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var area in Scanned)
            foreach (var file in LiteralScan.SourceFiles(Path.Combine(root, area)))
            {
                var hits = LiteralScan.Of(File.ReadAllText(file)).Count(l => LiteralScan.SqlToken(l.Text) is not null);
                if (hits > 0) counts[Path.GetRelativePath(root, file).Replace('\\', '/')] = hits;
            }
        return counts;
    }

    private static Dictionary<string, int> Recorded()
    {
        var path = Path.Combine(RuntimeSources.LibraryRoot(), "tests", "Tests", Baseline);
        var recorded = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(path).Where(l => l.Length > 0))
        {
            var split = line.Split('|');
            recorded[split[0]] = int.Parse(split[1], System.Globalization.CultureInfo.InvariantCulture);
        }
        return recorded;
    }

    [Fact(DisplayName = "[heuristic] no file of the core or the generator holds more SQL-shaped string literals than the recorded legacy baseline, and a file leaves the baseline the moment it is clean")]
    public void Core_and_generator_literals_carry_no_sql_beyond_the_baseline()
    {
        var actual = Counts();
        var recorded = Recorded();

        var problems = new List<string>();
        foreach (var (file, count) in actual)
        {
            if (!recorded.TryGetValue(file, out var allowed)) problems.Add(file + ": " + count + " SQL-shaped literal(s), none allowed");
            else if (count > allowed) problems.Add(file + ": " + count + " SQL-shaped literal(s), baseline " + allowed);
            else if (count < allowed) problems.Add(file + ": " + count + " SQL-shaped literal(s), baseline " + allowed + "; lower the baseline");
        }
        foreach (var file in recorded.Keys.Where(f => !actual.ContainsKey(f)))
            problems.Add(file + ": clean now; remove it from the baseline");

        Assert.True(problems.Count == 0,
            string.Join("\n", problems) + "\nbaseline as measured:\n"
            + string.Join("\n", actual.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "|" + p.Value)));
    }

    [Fact(DisplayName = "[heuristic] the node tree, the statement pipeline and the capability model are scanned and carry no SQL-shaped literal at all, and none of them is in the baseline")]
    public void The_node_model_is_clean_and_outside_the_baseline()
    {
        var root = RuntimeSources.LibraryRoot();
        var modelFiles = Directory.GetFiles(Path.Combine(root, "src", "Runtime", "Nodes"), "*.cs").ToList();
        foreach (var name in new[] { "Statement.cs", "StatementCompiler.cs", "ISerializer.cs", "ISqlProvider.cs", "Capability.cs", "CapabilityNotSupportedException.cs" })
            modelFiles.Add(RuntimeSources.Of(name));

        Assert.True(modelFiles.Count >= 12);
        var recorded = Recorded();
        foreach (var file in modelFiles)
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            Assert.False(recorded.ContainsKey(relative), relative + " must not be in the baseline");
            var hits = LiteralScan.Of(File.ReadAllText(file)).Where(l => LiteralScan.SqlToken(l.Text) is not null).Select(l => l.Text).ToList();
            Assert.True(hits.Count == 0, relative + ": " + string.Join(" | ", hits));
        }
    }

    [Fact(DisplayName = "[heuristic] the scan reports a planted SQL literal in a node-shaped class, a concatenated one and an interpolated one")]
    public void The_scan_catches_planted_literals()
    {
        const string source = """
            class PlantedNode
            {
                const string Direct = "SELECT 1 FROM t";
                const string Joined = "ORDER " + "BY id";
                string Interpolated(int n) => $"count(*) {n}";
                const string Spelled = "now()";
                const string Keyword = " AND ";
            }
            """;

        var tokens = LiteralScan.Of(source).Select(l => LiteralScan.SqlToken(l.Text)).ToList();

        Assert.Equal(5, tokens.Count);
        Assert.All(tokens, t => Assert.NotNull(t));
    }

    [Fact(DisplayName = "[heuristic] prose messages and plain words are not reported, so the gate does not fail on error text")]
    public void The_scan_ignores_prose_and_plain_words()
    {
        const string source = """
            class Prose
            {
                const string A = "a statement emitted anyway would carry syntax to a server that cannot parse it";
                const string B = "select the affected ids first, inside the same transaction";
                const string C = "Query<T> compares the extracted value instead";
                const string D = "name";
                const string E = "a_b";
            }
            """;

        var literals = LiteralScan.Of(source);

        Assert.Equal(5, literals.Count);
        Assert.All(literals, l => Assert.Null(LiteralScan.SqlToken(l.Text)));
    }

    [Theory(DisplayName = "[heuristic] data-literal shapes (a format string, a JSON path) are exempt, and a literal that only imitates them is not")]
    [InlineData("%Y-%m-%dT%H:%M:%f", true)]
    [InlineData("%Y-%m-%d %H:%M:%S", true)]
    [InlineData("$.name", true)]
    [InlineData("$.items[0].id", true)]
    [InlineData("$", true)]
    [InlineData("SELECT %s", false)]
    [InlineData("$.name FROM t", false)]
    [InlineData("%Y-%m-%d WHERE x", false)]
    [InlineData("count(*)", false)]
    public void Data_literal_shapes_are_exempt(string text, bool exempt)
    {
        Assert.Equal(exempt, LiteralScan.IsDataLiteral(text));
    }

    [Fact(DisplayName = "[heuristic] a literal that imitates a data shape but carries SQL is still reported, and a real data literal never is")]
    public void An_imitation_is_reported_and_a_data_literal_is_not()
    {
        Assert.Null(LiteralScan.SqlToken("%Y-%m-%dT%H:%M:%f"));
        Assert.Null(LiteralScan.SqlToken("$.where"));
        Assert.NotNull(LiteralScan.SqlToken("%Y-%m-%d WHERE x"));
        Assert.NotNull(LiteralScan.SqlToken("SELECT %s"));
        Assert.NotNull(LiteralScan.SqlToken("count(*)"));
    }
}
