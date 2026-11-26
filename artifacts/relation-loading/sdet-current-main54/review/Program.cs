using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
var inputs = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(args[0]));
var rows = new List<object>();
static string Tokens(SyntaxNode node) => string.Join("\n", node.DescendantTokens().Select(t => t.RawKind + ":" + t.Text));
foreach (var input in inputs.EnumerateArray())
{
    var oldPath = input.GetProperty("old").GetString()!;
    var actualPath = input.GetProperty("actual").GetString()!;
    var before = CSharpSyntaxTree.ParseText(File.ReadAllText(oldPath)).GetCompilationUnitRoot();
    var after = CSharpSyntaxTree.ParseText(File.ReadAllText(actualPath)).GetCompilationUnitRoot();
    var type = after.DescendantNodes().OfType<ClassDeclarationSyntax>()
        .Single(t => t.Parent is BaseNamespaceDeclarationSyntax);
    var added = type.Members.OfType<MethodDeclarationSyntax>().Where(m => m.ParameterList.Parameters.Any(
        p => p.Type!.ToString().Contains("RelationReadOptions", StringComparison.Ordinal))).ToArray();
    if (added.Length < 2 || added.Count(m => m.Identifier.ValueText == "GetAsync") != 2)
        throw new InvalidOperationException("Missing exact2Get additions: " + actualPath);
    var stripped = after.RemoveNodes(added, SyntaxRemoveOptions.KeepNoTrivia)!;
    if (Tokens(stripped) != Tokens(before)) throw new InvalidOperationException("Legacy token delta: " + actualPath);
    var oldMaps = before.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText == "Map");
    var maps = after.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText == "Map");
    if (!oldMaps.Select(m => m.ToFullString()).SequenceEqual(maps.Select(m => m.ToFullString())))
        throw new InvalidOperationException("Map byte delta: " + actualPath);
    foreach (var method in added)
    {
        var parameters = method.ParameterList.Parameters;
        if (parameters.Count > 5 || parameters[^2].Identifier.ValueText != "options"
            || parameters[^1].Identifier.ValueText != "ct" || parameters[^1].Default?.Value.ToString() != "default")
            throw new InvalidOperationException("Unexpected added signature: " + method);
        if (method.Identifier.ValueText != "GetAsync" && !method.Identifier.ValueText.StartsWith("Load"))
            throw new InvalidOperationException("Unexpected added subject: " + method);
        if (method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(i =>
            i.Expression.ToString().EndsWith(".On", StringComparison.Ordinal)))
            throw new InvalidOperationException("Connection bridge encoded explicit On: " + method);
    }
    rows.Add(new { Old = oldPath, Actual = actualPath, Type = type.Identifier.ValueText,
        LegacyTokensExact = true, MapFullTextExact = true,
        Additions = added.Select(m => new { Name = m.Identifier.ValueText, Source = m.ToFullString() }) });
}
File.WriteAllText(args[1], JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Reviewed {rows.Count} generatedfiles: alloldtokens+Mapfulltext exact, only options Get/Load additions.");
