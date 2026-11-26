using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class MetricScan
{
    internal static object FileOf(string file)
    {
        var text = File.ReadAllText(file);
        var tree = CSharpSyntaxTree.ParseText(text);
        var root = tree.GetRoot();
        var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Where(t => t is ClassDeclarationSyntax or InterfaceDeclarationSyntax or RecordDeclarationSyntax)
            .Where(t => !t.Ancestors().OfType<TypeDeclarationSyntax>().Any())
            .Select(t => new { kind = t.Kind().ToString(), name = t.Identifier.Text,
                start = Line(t), end = End(t), codeLines = CodeLines(t),
                primaryParameters = t is ClassDeclarationSyntax c ? c.ParameterList?.Parameters.Count
                    : t is RecordDeclarationSyntax r ? r.ParameterList?.Parameters.Count : null }).ToArray();
        var methods = root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>().Select(MethodOf).ToArray();
        var longLines = text.Split('\n').Select((line, i) => new { line = i + 1, length = line.TrimEnd('\r').Length })
            .Where(line => line.length > 120).ToArray();
        return new { file, types, methods, longLines };
    }

    private static object MethodOf(BaseMethodDeclarationSyntax method)
    {
        var points = method.DescendantNodes().Where(IsDecision)
            .Select(node => new { kind = node.Kind().ToString(), line = Line(node) }).ToArray();
        var nesting = method.DescendantNodes().OfType<BlockSyntax>()
            .Select(block => new { line = Line(block), depth = block.Ancestors().OfType<BlockSyntax>()
                .TakeWhile(parent => method.Span.Contains(parent.Span)).Count() })
            .OrderByDescending(block => block.depth).FirstOrDefault();
        var name = method is MethodDeclarationSyntax named ? named.Identifier.Text : method.Kind().ToString();
        return new { name, start = Line(method), end = End(method), codeLines = CodeLines(method),
            parameters = method.ParameterList.Parameters.Count, complexity = points.Length + 1, points, nesting };
    }

    private static bool IsDecision(SyntaxNode node) => node is IfStatementSyntax or ForStatementSyntax
        or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax or CatchClauseSyntax
        or ConditionalExpressionSyntax or CaseSwitchLabelSyntax or CasePatternSwitchLabelSyntax
        || node is BinaryExpressionSyntax binary && (binary.IsKind(SyntaxKind.LogicalAndExpression)
            || binary.IsKind(SyntaxKind.LogicalOrExpression) || binary.IsKind(SyntaxKind.CoalesceExpression));

    private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    private static int End(SyntaxNode node) => node.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
    private static int CodeLines(SyntaxNode node) => node.DescendantTokens().SelectMany(token =>
        Enumerable.Range(token.GetLocation().GetLineSpan().StartLinePosition.Line,
            token.GetLocation().GetLineSpan().EndLinePosition.Line
            - token.GetLocation().GetLineSpan().StartLinePosition.Line + 1)).Distinct().Count();
}
