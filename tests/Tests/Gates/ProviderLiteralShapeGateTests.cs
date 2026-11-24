using Xunit;

namespace Neemle.OrmLite.Tests.Gates;

public sealed class ProviderLiteralShapeGateTests
{
    private static readonly string[] RuleAreas =
    [
        "src/Sql",
        "src/Postgres",
        "src/Sqlite",
    ];

    private const string TokenLayer = "SqlWriter.cs";

    private const string NamedConstants = "DataLiterals.cs";

    private static bool HoldsMoreThanOneToken(string text) =>
        text.Any(c => char.IsWhiteSpace(c) || c is '\'' or '"' or '(' or ')');

    private static List<string> Violations(string source) =>
        LiteralScan.Of(source)
            .Where(l => !l.InsideExceptionMessage && !LiteralScan.IsDataLiteral(l.Text) && HoldsMoreThanOneToken(l.Text))
            .Select(l => "line " + l.Line + ": " + l.Text)
            .ToList();

    [Fact(DisplayName = "[heuristic] a rule file of the SQL kit or a provider writes only single-token literals: none holds whitespace, a quote or a parenthesis, apart from exception text, the token writer and the named data-literal classes")]
    public void Rule_files_write_single_tokens()
    {
        var root = RuntimeSources.LibraryRoot();
        var checkedFiles = 0;
        var problems = new List<string>();
        foreach (var area in RuleAreas)
            foreach (var file in LiteralScan.SourceFiles(Path.Combine(root, area)))
            {
                var name = Path.GetFileName(file);
                if (name == TokenLayer || name.EndsWith(NamedConstants, StringComparison.Ordinal)) continue;
                checkedFiles++;
                foreach (var violation in Violations(File.ReadAllText(file)))
                    problems.Add(Path.GetRelativePath(root, file) + " " + violation);
            }

        Assert.True(checkedFiles >= 4, "only " + checkedFiles + " rule files were scanned");
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact(DisplayName = "[heuristic] the shape check reports a multi-token literal, a quoted one and a parenthesised one planted in a rule file, and passes single tokens, exception text and data shapes")]
    public void The_shape_check_catches_planted_literals()
    {
        const string source = """
            class Rule
            {
                void Bad(Writer w)
                {
                    w.Atom("COALESCE(x, 0)");
                    w.Atom("a b");
                    w.Atom("'x'");
                    w.Atom("now(");
                }

                void Good(Writer w)
                {
                    w.Atom("pg_input_is_valid");
                    w.Atom("->>");
                    w.Atom("%Y-%m-%dT%H:%M:%f");
                    w.Atom("$.key");
                    throw new System.NotSupportedException("a message with spaces");
                }
            }
            """;

        var violations = Violations(source);

        Assert.Equal(4, violations.Count);
        Assert.DoesNotContain(violations, v => v.Contains("pg_input_is_valid", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "the named data-literal class of the Postgres provider is the only place a multi-token literal may live, and it holds nothing but constants")]
    public void The_named_constants_class_holds_only_constants()
    {
        var root = RuntimeSources.LibraryRoot();
        var file = Path.Combine(root, "src", "Postgres", "PostgresDataLiterals.cs");

        var text = File.ReadAllText(file);

        Assert.Contains("internal static class PostgresDataLiterals", text, StringComparison.Ordinal);
        Assert.DoesNotContain("static void", text, StringComparison.Ordinal);
        Assert.DoesNotContain("=>", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "every named data-literal class of a provider, the history ones included, holds only constants and no method")]
    public void Every_named_constants_class_holds_only_constants()
    {
        var root = RuntimeSources.LibraryRoot();
        var files = RuleAreas
            .SelectMany(area => LiteralScan.SourceFiles(Path.Combine(root, area)))
            .Where(f => Path.GetFileName(f).EndsWith(NamedConstants, StringComparison.Ordinal))
            .ToList();

        Assert.True(files.Count >= 5, "only " + files.Count + " data-literal classes were found");
        foreach (var file in files)
        {
            var tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            var members = tree.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>().SelectMany(c => c.Members).ToList();
            Assert.All(members, m => Assert.True(
                m is Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax f && f.Modifiers.Any(x => x.Text == "const"),
                Path.GetFileName(file) + ": " + m.ToString().Split('\n')[0]));
        }
    }

    [Fact(DisplayName = "the named data-literal class of the SQLite provider holds nothing but constants")]
    public void The_sqlite_constants_class_holds_only_constants()
    {
        var root = RuntimeSources.LibraryRoot();
        var file = Path.Combine(root, "src", "Sqlite", "SqliteDataLiterals.cs");

        var text = File.ReadAllText(file);

        Assert.Contains("internal static class SqliteDataLiterals", text, StringComparison.Ordinal);
        Assert.DoesNotContain("static void", text, StringComparison.Ordinal);
        Assert.DoesNotContain("=>", text, StringComparison.Ordinal);
    }
}
