using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Metrics;

internal static class CodeCounts
{
    internal static int Lines(SyntaxNode node) => node.DescendantTokens().SelectMany(t =>
    {
        var span = t.GetLocation().GetLineSpan();
        return Enumerable.Range(span.StartLinePosition.Line,
            span.EndLinePosition.Line - span.StartLinePosition.Line + 1);
    }).Distinct().Count();

    internal static bool Bound(SyntaxNode node) => node is ClassDeclarationSyntax or InterfaceDeclarationSyntax
        or RecordDeclarationSyntax;

    internal static IReadOnlyList<SyntaxNode> Functions(SyntaxNode root) => root.DescendantNodes().Where(n =>
        n is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax or AccessorDeclarationSyntax
        || n is PropertyDeclarationSyntax { ExpressionBody: not null }).ToArray();

    internal static string Name(SyntaxNode node) => node switch
    {
        MethodDeclarationSyntax m => m.Identifier.ValueText,
        ConstructorDeclarationSyntax c => c.Identifier.ValueText,
        LocalFunctionStatementSyntax f => f.Identifier.ValueText,
        PropertyDeclarationSyntax p => p.Identifier.ValueText,
        AccessorDeclarationSyntax a => a.Keyword.ValueText,
        _ => node.Kind().ToString(),
    };

    internal static int Parameters(SyntaxNode node) => node switch
    {
        BaseMethodDeclarationSyntax m => m.ParameterList.Parameters.Count,
        LocalFunctionStatementSyntax f => f.ParameterList.Parameters.Count,
        _ => 0,
    };
}
