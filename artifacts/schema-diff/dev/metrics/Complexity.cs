using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Metrics;

internal static class Complexity
{
    internal static IReadOnlyList<SyntaxNode> Decisions(SyntaxNode node) => node.DescendantNodes().Where(n =>
        !n.Ancestors().TakeWhile(a => a != node).OfType<LocalFunctionStatementSyntax>().Any() && Decision(n)).ToArray();

    private static bool Decision(SyntaxNode node) => node switch
    {
        IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax
            or DoStatementSyntax or CatchClauseSyntax or ConditionalExpressionSyntax or CaseSwitchLabelSyntax => true,
        SwitchExpressionArmSyntax arm => arm.Pattern is not DiscardPatternSyntax,
        BinaryExpressionSyntax binary => binary.Kind() is SyntaxKind.LogicalAndExpression
            or SyntaxKind.LogicalOrExpression or SyntaxKind.CoalesceExpression,
        _ => false,
    };

    internal static object Nesting(SyntaxNode node)
    {
        var max = node.DescendantNodes().OfType<BlockSyntax>().Select(b => new
        {
            Line = b.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            Depth = b.Ancestors().TakeWhile(a => a != node).OfType<BlockSyntax>().Count(),
        }).OrderByDescending(x => x.Depth).FirstOrDefault();
        return max is null ? new { Line = 0, Depth = 0 } : max;
    }
}
