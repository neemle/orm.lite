using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

var oldRoot = Path.GetFullPath(args[0]);
var currentRoot = Path.GetFullPath(args[1]);
var result = new List<object>();
foreach (var name in new[] { "GraphAuthorOrm", "GraphBookOrm", "GraphTagOrm" })
{
    var old = Find(oldRoot, name);
    var current = Find(currentRoot, name);
    if (!Tokens(old.Type).SequenceEqual(Tokens(current.Type))) throw new InvalidOperationException(name + " tokens changed");
    var map = old.Type.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Map");
    var next = current.Type.Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == "Map");
    if (map.ToFullString() != next.ToFullString()) throw new InvalidOperationException(name + " Map changed");
    result.Add(new { Type = name, Old = old.Path, Current = current.Path, TokensEqual = true, MapTextEqual = true,
        OldSha256 = Hash(old.Path), CurrentSha256 = Hash(current.Path) });
}
File.WriteAllText(args[2], JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("ORIGINAL_GRAPH_GENERATED_BRIDGE 3/3 type tokens and Map full text unchanged");

static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
static string[] Tokens(SyntaxNode node) => node.DescendantTokens().Select(t => t.RawKind + ":" + t.Text).ToArray();
static (string Path, ClassDeclarationSyntax Type) Find(string directory, string name)
{
    var matches = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
        .SelectMany(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot().DescendantNodes()
            .OfType<ClassDeclarationSyntax>().Where(type => type.Identifier.ValueText == name)
            .Select(type => (Path: path, Type: type))).ToArray();
    return matches.Length == 1 ? matches[0] : throw new InvalidOperationException(name + " source is not unique");
}
