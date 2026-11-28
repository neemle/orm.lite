using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Neemle.OrmLite.Tests.Unit.Gates;

// ORML-150: every place where the generator turns emitted C# text into a Roslyn tree or reformats one, instead of
// building the code through Neemle.CSharpComposer, is a recorded exception with a reason in
// generator-roslyn-parse-exceptions.txt, and this census keeps that record exact. It reads syntax only: a call
// counts when it is spelled CSharpSyntaxTree.Parse*, SyntaxFactory.Parse* (or a bare Parse* under a static import of
// either) or *.NormalizeWhitespace().
public sealed class GeneratorRoslynParseCensusTests
{
    private const string ListName = "generator-roslyn-parse-exceptions.txt";

    private static readonly string[] Parsers = ["CSharpSyntaxTree", "SyntaxFactory"];

    internal static string Root()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "Neemle.OrmLite.slnx"))) return d.FullName;
        throw new InvalidOperationException("Neemle.OrmLite.slnx not found above the test assembly");
    }

    // The calls of one source, as "<call>" keys: "CSharpSyntaxTree.ParseText", "SyntaxFactory.ParseStatement",
    // ".NormalizeWhitespace". A receiver may be spelled through a using alias (this file's or a global one of another
    // file, in `aliases`), and the text of every inactive #if branch is read too, so no build configuration hides a
    // site.
    internal static IEnumerable<string> Calls(string text, IReadOnlyDictionary<string, string>? aliases = null)
    {
        var root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
        var names = new Dictionary<string, string>(aliases ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        foreach (var (alias, parser) in Aliases(root)) names[alias] = parser;
        foreach (var parser in Parsers) names[parser] = parser;
        var imported = root.Usings.Where(u => u.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
            .Select(u => u.Name!.ToString().Split('.').Last()).Where(Parsers.Contains).ToArray();
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            switch (call.Expression)
            {
                case MemberAccessExpressionSyntax { Name.Identifier.ValueText: "NormalizeWhitespace" }:
                    yield return ".NormalizeWhitespace";
                    break;
                case MemberAccessExpressionSyntax access
                    when access.Name.Identifier.ValueText.StartsWith("Parse", StringComparison.Ordinal)
                    && names.TryGetValue(access.Expression.ToString().Split('.').Last(), out var parser):
                    yield return parser + "." + access.Name.Identifier.ValueText;
                    break;
                case IdentifierNameSyntax name when imported.Length > 0
                    && name.Identifier.ValueText.StartsWith("Parse", StringComparison.Ordinal):
                    yield return imported[0] + "." + name.Identifier.ValueText;
                    break;
            }
        }
        foreach (var disabled in root.DescendantTrivia().Where(t => t.IsKind(SyntaxKind.DisabledTextTrivia)))
            foreach (var inner in Calls(disabled.ToString(), names)) yield return inner;
    }

    // `using X = ...CSharpSyntaxTree;` or `...SyntaxFactory;`: alias -> parser; only the global ones when asked.
    internal static IEnumerable<(string Alias, string Parser)> Aliases(CompilationUnitSyntax root, bool globalOnly = false) =>
        root.Usings.Where(u => u.Alias is not null && Parsers.Contains(u.Name!.ToString().Split('.').Last())
                && (!globalOnly || u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)))
            .Select(u => (u.Alias!.Name.Identifier.ValueText, u.Name!.ToString().Split('.').Last()));

    // "<path>\t<call>" -> count, over the files of src/Generator (bin and obj excluded).
    internal static Dictionary<string, int> Census(string generator)
    {
        var files = Directory.GetFiles(generator, "*.cs", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(generator, f).Replace('\\', '/'))
            .Where(f => !f.StartsWith("bin/", StringComparison.Ordinal) && !f.StartsWith("obj/", StringComparison.Ordinal))
            .ToDictionary(f => f, f => File.ReadAllText(Path.Combine(generator, f)), StringComparer.Ordinal);
        var global = files.Values
            .SelectMany(t => Aliases(CSharpSyntaxTree.ParseText(t).GetCompilationUnitRoot(), globalOnly: true))
            .GroupBy(a => a.Alias, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Parser, StringComparer.Ordinal);
        return files.SelectMany(f => Calls(f.Value, global).Select(c => f.Key + "\t" + c))
            .GroupBy(k => k, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
    }

    // "<path>\t<call>" -> (count, reason) from the list; a line without a reason is a problem.
    internal static Dictionary<string, (int Count, string Reason)> Recorded(IEnumerable<string> lines, List<string> problems)
    {
        var recorded = new Dictionary<string, (int, string)>(StringComparer.Ordinal);
        foreach (var line in lines.Where(l => l.Length > 0 && !l.StartsWith('#')))
        {
            var parts = line.Split('\t');
            if (parts.Length != 4 || !int.TryParse(parts[2], out var count) || count <= 0
                || parts[3].Trim().Length == 0 || !recorded.TryAdd(parts[0] + "\t" + parts[1], (count, parts[3])))
                problems.Add("malformed or repeated line: " + line);
        }
        return recorded;
    }

    internal static List<string> Compare(Dictionary<string, int> found, IEnumerable<string> listLines)
    {
        var problems = new List<string>();
        var recorded = Recorded(listLines, problems);
        foreach (var (site, count) in found.OrderBy(p => p.Key, StringComparer.Ordinal))
            if (!recorded.TryGetValue(site, out var entry)) problems.Add("not recorded: " + site + " x" + count);
            else if (entry.Count != count) problems.Add("recorded x" + entry.Count + " but found x" + count + ": " + site);
        foreach (var site in recorded.Keys.Where(k => !found.ContainsKey(k)).Order(StringComparer.Ordinal))
            problems.Add("recorded but gone (take the line away): " + site);
        return problems;
    }

    [Fact(DisplayName = "every Roslyn parse or reformat of emitted text in the generator is a recorded exception with a reason")]
    public void Every_roslyn_parse_site_of_the_generator_is_recorded()
    {
        var root = Root();
        var list = Path.Combine(root, "tests", "Unit", "Neemle.OrmLite.Tests.Unit", "Gates", ListName);

        var problems = Compare(Census(Path.Combine(root, "src", "Generator")), File.ReadAllLines(list));

        Assert.True(problems.Count == 0, ListName + " does not match src/Generator:\n" + string.Join("\n", problems));
    }

    [Theory(DisplayName = "the census finds each spelling of a Roslyn parse or reformat")]
    [InlineData("class C { void M(string s) { var t = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(s); } }", "CSharpSyntaxTree.ParseText")]
    [InlineData("class C { void M(string s) { var t = SyntaxFactory.ParseExpression(s); } }", "SyntaxFactory.ParseExpression")]
    [InlineData("using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory; class C { void M(string s) { var t = ParseTypeName(s); } }", "SyntaxFactory.ParseTypeName")]
    [InlineData("class C { void M(Microsoft.CodeAnalysis.SyntaxNode n) { n = n.NormalizeWhitespace(); } }", ".NormalizeWhitespace")]
    [InlineData("using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory; class C { void M(string s) { var t = SF.ParseExpression(s); } }", "SyntaxFactory.ParseExpression")]
    [InlineData("class C { void M(string s) {\n#if NET10_0\n var t = SyntaxFactory.ParseExpression(s);\n#endif\n } }", "SyntaxFactory.ParseExpression")]
    public void The_census_finds_each_spelling(string source, string expected) =>
        Assert.Equal(new[] { expected }, Calls(source));

    [Fact(DisplayName = "a global alias declared in one file reaches a call in another")]
    public void A_global_alias_reaches_another_file()
    {
        var aliases = Aliases(CSharpSyntaxTree.ParseText(
            "global using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;").GetCompilationUnitRoot(), globalOnly: true)
            .ToDictionary(a => a.Alias, a => a.Parser);

        Assert.Equal(new[] { "SyntaxFactory.ParseName" },
            Calls("class C { void M(string s) { _ = SF.ParseName(s); } }", aliases));
    }

    [Theory(DisplayName = "the census leaves alone what is not a Roslyn parse")]
    [InlineData("class C { static int Parse(string s) => 0; void M() { _ = Parse(\"1\"); _ = int.Parse(\"1\"); } }")]
    [InlineData("class C { void M() { _ = Neemle.CSharpComposer.Cs.Tree(); } }")]
    public void The_census_leaves_other_calls_alone(string source) => Assert.Empty(Calls(source));

    [Fact(DisplayName = "an unrecorded site, a wrong count, a gone site and a line without a reason are each reported")]
    public void Each_mismatch_is_reported()
    {
        var found = new Dictionary<string, int>(StringComparer.Ordinal)
        { ["A.cs\tSyntaxFactory.ParseStatement"] = 2, ["B.cs\t.NormalizeWhitespace"] = 1 };
        string[] list =
        [
            "# comment", "",
            "A.cs\tSyntaxFactory.ParseStatement\t1\treason",
            "C.cs\tCSharpSyntaxTree.ParseText\t1\treason",
            "D.cs\tCSharpSyntaxTree.ParseText\t1\t ",
        ];

        var problems = Compare(found, list);

        Assert.Equal(new[]
        {
            "malformed or repeated line: D.cs\tCSharpSyntaxTree.ParseText\t1\t ",
            "recorded x1 but found x2: A.cs\tSyntaxFactory.ParseStatement",
            "not recorded: B.cs\t.NormalizeWhitespace x1",
            "recorded but gone (take the line away): C.cs\tCSharpSyntaxTree.ParseText",
        }, problems);
    }
}
