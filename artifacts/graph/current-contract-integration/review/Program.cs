using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var rows = JsonSerializer.Deserialize<List<ChangedFile>>(File.ReadAllText(args[0]))!;
var results = new List<object>();
foreach (var row in rows)
{
    var old = CSharpSyntaxTree.ParseText(File.ReadAllText(row.Old)).GetCompilationUnitRoot();
    var current = CSharpSyntaxTree.ParseText(File.ReadAllText(row.New)).GetCompilationUnitRoot();
    Require(!old.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error), row.Old + " old syntax");
    Require(!current.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error), row.New + " syntax");
    var oldMaps = Methods(old, "Map").Select(m => m.ToFullString()).ToArray();
    var maps = Methods(current, "Map").Select(m => m.ToFullString()).ToArray();
    Require(oldMaps.SequenceEqual(maps), row.Old + " Map text changed");
    var entities = Methods(current, "RegisterEntities").SingleOrDefault();
    if (entities is not null) CheckInitializer(old, current, entities, row.Old);
    var normalized = new RemoveGraph(entities?.Body).Visit(current)!;
    Require(Tokens(old).SequenceEqual(Tokens(normalized)), row.Old + " non-graph tokens changed");
    results.Add(new { row.Old, row.New, MapCount = maps.Length, MapTextUnchanged = true,
        LegacyTokensUnchanged = true, Registrations = Methods(current, "RegisterGraph").Count(),
        Paths = current.DescendantNodes().OfType<ClassDeclarationSyntax>().Count(c => c.Identifier.ValueText == "Paths"),
        InitializationExtracted = entities is not null });
}
File.WriteAllText(args[1], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("REVIEWED " + rows.Count + " changed generated files: legacy tokens and Map text preserved");

static IEnumerable<MethodDeclarationSyntax> Methods(SyntaxNode root, string name) =>
    root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText == name);
static string[] Tokens(SyntaxNode node) => node.DescendantTokens().Select(t => t.RawKind + ":" + t.Text).ToArray();
static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void CheckInitializer(SyntaxNode old, SyntaxNode current, MethodDeclarationSyntax entities, string file)
{
    var init = Methods(current, "Init").Single();
    var prior = Methods(old, "Init").Single();
    Require(Tokens(prior.Body!).SequenceEqual(Tokens(entities.Body!)), file + " registration order changed");
    var calls = init.Body!.Statements.Select(s => s.ToString()).ToArray();
    Require(calls.SequenceEqual(new[] { "RegisterEntities();", "RegisterGraphs();" }), file + " Init calls changed");
    var graphs = Methods(current, "RegisterGraphs").Single();
    Require(graphs.Body!.Statements.All(s => s is ExpressionStatementSyntax
        { Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member } }
        && member.Name.Identifier.ValueText == "RegisterGraph"), file + " unexpected graph Init statement");
}
record ChangedFile(string Old, string New);
sealed class RemoveGraph(BlockSyntax? entities) : CSharpSyntaxRewriter
{
    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node) =>
        node.Identifier.ValueText == "Paths" ? null : base.VisitClassDeclaration(node);
    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Identifier.ValueText is "RegisterGraph" or "RegisterEntities" or "RegisterGraphs") return null;
        if (entities is not null && node.Identifier.ValueText == "Init"
            && node.Parent is ClassDeclarationSyntax { Identifier.ValueText: "OrmMetaInit" })
            return node.WithBody(entities);
        return base.VisitMethodDeclaration(node);
    }
}
