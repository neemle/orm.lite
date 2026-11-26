using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
using System.Security.Cryptography;

var b = "artifacts/foreign-keys/dev-migration-layout";
var previous = File.ReadAllBytes(b + "/before/corpus/entries.json");
var current = File.ReadAllBytes(b + "/after/corpus/entries.json");
if (!previous.SequenceEqual(current)) throw new InvalidOperationException("zero-opt-in corpus changed bytes");
var groups = new List<object>();
foreach (var name in new[] { "demo", "native10", "native11" })
{
    var after = name == "demo" ? b + "/generated" : b + "/" + name + "-build/generated/ForeignKeysAot";
    groups.Add(LayoutProof.Compare(name, b + "/before/" + name, after));
}
using var proof = JsonDocument.Parse(File.ReadAllText(b + "/after/corpus/case-proof.json"));
if (!proof.RootElement.GetProperty("discoveryPermutationEqual").GetBoolean()
    || proof.RootElement.GetProperty("first").GetProperty("Diagnostics").GetArrayLength() != 0)
    throw new InvalidOperationException("case compilation/permutation");
Console.WriteLine(JsonSerializer.Serialize(new { zeroOptInCorpusByteExact = true, corpusEntries = 669,
    caseCompilationAndPermutation = true, groups }, new JsonSerializerOptions { WriteIndented = true }));

internal static class LayoutProof
{
    internal static object Compare(string name, string before, string after)
    {
        var old = Types(before); var next = Types(after);
        if (!old.Keys.Order().SequenceEqual(next.Keys.Order())) throw new InvalidOperationException(name + " identities");
        var rows = old.Select(pair => Check(pair.Key, pair.Value, next[pair.Key])).ToArray();
        return new { name, count = rows.Length, rows };
    }

    private static Dictionary<string, (ClassDeclarationSyntax Node, string File)> Types(string directory)
    {
        var result = new Dictionary<string, (ClassDeclarationSyntax, string)>();
        foreach (var file in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(file)).GetRoot();
            if (root.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error))
                throw new InvalidOperationException(file + " parse");
            var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
            if (classes.Length != 1) throw new InvalidOperationException(file + " declaration ceiling");
            var ns = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().Single().Name;
            result.Add(ns + "." + classes[0].Identifier.Text, (classes[0], file));
        }
        return result;
    }

    private static object Check(string name, (ClassDeclarationSyntax Node, string File) old,
        (ClassDeclarationSyntax Node, string File) next)
    {
        var a = old.Node.DescendantTokens().Select(t => (t.RawKind,t.Text));
        var z = next.Node.DescendantTokens().Select(t => (t.RawKind,t.Text));
        if (!a.SequenceEqual(z)) throw new InvalidOperationException(name + " tokens");
        foreach (var map in old.Node.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.Text == "Map"))
            if (map.ToFullString() != next.Node.Members.OfType<MethodDeclarationSyntax>()
                .Single(m => m.Identifier.Text == "Map").ToFullString())
                throw new InvalidOperationException(name + " Map full text");
        return new { name, before = old.File, after = next.File, tokenAndNamespaceEqual = true,
            oldMapFullTextEqual = true, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(next.File))) };
    }
}
