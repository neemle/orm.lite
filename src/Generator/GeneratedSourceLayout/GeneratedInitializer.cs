using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.OrmLite.Generator.GeneratedSourceLayout;

/// <summary>Bounds a generated initializer while preserving its ordered registration statements.</summary>
internal static class GeneratedInitializer
{
    private const int Batch = 20;

    internal static TypeDeclarationSyntax Split(TypeDeclarationSyntax type)
    {
        var init = type.Members.OfType<MethodDeclarationSyntax>().SingleOrDefault(m =>
            m.Identifier.ValueText == "Init" && m.AttributeLists.Count != 0);
        if (init?.Body is not { } body || body.Statements.Count <= Batch) return type;
        var chunks = body.Statements.Select((statement, index) => (statement, index))
            .GroupBy(x => x.index / Batch).ToArray();
        var calls = chunks.Select((_, index) => SyntaxFactory.ParseStatement($"InitPart{index}();"));
        var replacement = Format(init.WithBody(SyntaxFactory.Block(calls)));
        var result = type.ReplaceNode(init, replacement);
        foreach (var chunk in chunks)
        {
            var method = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(
                $"private static void InitPart{chunk.Key}() {{ }}")!;
            result = result.AddMembers(Format(method.WithBody(SyntaxFactory.Block(chunk.Select(x => x.statement)))));
        }
        return result;
    }

    private static MethodDeclarationSyntax Format(MethodDeclarationSyntax method)
    {
        var lines = method.NormalizeWhitespace().ToFullString().Split('\n');
        var text = "\n" + string.Join("\n", lines.Select(line => "    " + line.TrimEnd('\r'))) + "\n";
        return (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(text)!;
    }
}
