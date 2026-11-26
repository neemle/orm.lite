using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var groups = new List<object>();
var basePath = "artifacts/streaming/dev-generated-repair";
foreach (var before in Directory.GetDirectories(basePath + "/before/corpus", "generator-*"))
{
    var name = Path.GetFileName(before);
    groups.Add(Proof.Compare(name, Directory.GetFiles(before, "*.g.cs.golden"),
        Directory.GetFiles(basePath + "/verified-after/corpus/" + name, "*.g.cs.golden")));
}
foreach (var major in new[] { "10", "11" })
{
    var after = basePath + "/verified-native" + major + "-build/generated/StreamingAot";
    groups.Add(Proof.Compare("native" + major, Directory.GetFiles(basePath + "/before/native" + major, "*.cs"),
        Directory.GetFiles(after, "*.cs", SearchOption.AllDirectories)));
}
Console.WriteLine(JsonSerializer.Serialize(groups, new JsonSerializerOptions { WriteIndented = true }));

static class Proof
{
    internal static object Compare(string name, string[] before, string[] after)
    {
        var oldTypes = Types(before); var newTypes = Types(after);
        Need(oldTypes.Keys.Order().SequenceEqual(newTypes.Keys.Order()), name + " type identities");
        var facts = new List<object>();
        foreach (var (key, old) in oldTypes)
        {
            var next = newTypes[key];
            var init = old.Node.Members.OfType<MethodDeclarationSyntax>().SingleOrDefault(m => m.Identifier.Text == "Init");
            var equal = Tokens(old.Node).SequenceEqual(Tokens(next.Node));
            if (!equal) Initializer(old.Node, next.Node);
            var maps = old.Node.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.Text == "Map");
            foreach (var map in maps) Need(map.ToFullString() == next.Node.Members.OfType<MethodDeclarationSyntax>()
                .Single(m => m.Identifier.Text == "Map").ToFullString(), name + " Map full text");
            facts.Add(new { type = key, old.File, beforeLine = Line(old.Node), afterFile = next.File,
                afterLine = Line(next.Node), nonTriviaTokensEqual = equal, initializerFlattenedEqual = !equal,
                mapFullTextEqual = true, oldTokenHash = Hash(Tokens(old.Node)) });
        }
        foreach (var file in after) Need(Types([file]).Count <= 1, file + " declaration ceiling");
        return new { name, beforeFiles = before.Length, afterFiles = after.Length, facts };
    }

    private static Dictionary<string, (TypeDeclarationSyntax Node, string File)> Types(string[] files)
    {
        var result = new Dictionary<string, (TypeDeclarationSyntax, string)>();
        foreach (var file in files)
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            Need(!root.ContainsDiagnostics, file + " syntax");
            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>()
                .Where(t => !t.Ancestors().OfType<TypeDeclarationSyntax>().Any()))
            {
                var ns = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Single().Name.ToString();
                result.Add(ns + "." + type.Identifier.Text, (type, file));
            }
        }
        return result;
    }

    private static void Initializer(TypeDeclarationSyntax before, TypeDeclarationSyntax after)
    {
        var old = before.Members.OfType<MethodDeclarationSyntax>().Single();
        var methods = after.Members.OfType<MethodDeclarationSyntax>().ToDictionary(m => m.Identifier.Text);
        var init = methods["Init"];
        Need(Tokens(old.WithBody(null)).SequenceEqual(Tokens(init.WithBody(null))), "Init signature/attributes");
        var flattened = Flatten(init, methods).SelectMany(Tokens);
        Need(old.Body!.Statements.SelectMany(Tokens).SequenceEqual(flattened), "exact registration order/tokens");
        foreach (var helper in methods.Values.Where(m => m.Identifier.Text != "Init"))
            Need(helper.Modifiers.ToString() == "private static" && helper.ReturnType.ToString() == "void"
                && helper.ParameterList.Parameters.Count == 0 && helper.AttributeLists.Count == 0, "private helper shape");
    }

    private static IEnumerable<StatementSyntax> Flatten(MethodDeclarationSyntax method,
        IReadOnlyDictionary<string, MethodDeclarationSyntax> methods)
    {
        foreach (var statement in method.Body!.Statements)
        {
            if (statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }
                && methods.TryGetValue(invocation.Expression.ToString(), out var helper))
                foreach (var item in Flatten(helper, methods)) yield return item;
            else yield return statement;
        }
    }

    private static IEnumerable<string> Tokens(SyntaxNode node) =>
        node.DescendantTokens().Select(t => t.RawKind + ":" + t.Text);
    private static string Hash(IEnumerable<string> tokens) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", tokens))));
    private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    private static void Need(bool value, string label)
    { if (!value) throw new InvalidOperationException(label); }
}
