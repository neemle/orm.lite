using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.Orm.Lite.Tests.Gates;

internal sealed record Literal(string Text, int Line, bool InsideExceptionMessage);

internal static class LiteralScan
{
    private static readonly Regex JsonPath = new(@"\A\$(\.[A-Za-z_][A-Za-z0-9_]*|\[\d+\])*\z", RegexOptions.CultureInvariant);

    private static readonly Regex FormatDirective = new(@"%[A-Za-z%]", RegexOptions.CultureInvariant);

    private static readonly Regex FormatShape = new(@"\A(%[A-Za-z%]|[-:T. /,Z0-9])+\z", RegexOptions.CultureInvariant);

    private static readonly Regex[] Phrases =
    [
        new(@"\b(SELECT|INSERT|DELETE|UPDATE|CREATE|ALTER|DROP|TRUNCATE)\b.*\b(FROM|INTO|SET|TABLE|INDEX|COLUMN|CONSTRAINT|TRIGGER|FUNCTION)\b", RegexOptions.CultureInvariant | RegexOptions.Singleline),
        new(@"\b(ON CONFLICT|ORDER BY|GROUP BY|LEFT JOIN|INNER JOIN|UNION ALL|FOR UPDATE|FOR SHARE|FOR NO KEY UPDATE|IS NOT NULL|IS NULL|IS NOT DISTINCT FROM|DO NOTHING|DO UPDATE SET)\b", RegexOptions.CultureInvariant),
        new(@"\b(SELECT|INSERT|UPDATE|DELETE|WHERE|FROM|ILIKE|RETURNING|VALUES|DISTINCT)\b", RegexOptions.CultureInvariant),
        new(@"\b(count|coalesce|lower|upper|now|min|max|sum|array_position|to_jsonb|make_interval|set_config|to_regclass|current_setting)\s*\(", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase),
        new(@"\b(jsonb_[a-z_]+|pg_[a-z_]+)\b", RegexOptions.CultureInvariant),
        new(@"::[a-z]|->>|@>|@p[0-9]|\$[0-9]", RegexOptions.CultureInvariant),
    ];

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "SELECT", "FROM", "WHERE", "AND", "OR", "NOT", "IN", "IS", "NULL", "LIKE", "ORDER", "BY", "LIMIT", "OFFSET",
        "AS", "ASC", "DESC", "CASE", "WHEN", "THEN", "ELSE", "END", "DISTINCT", "EXISTS", "COUNT", "SUM", "MIN", "MAX",
        "INSERT", "INTO", "VALUES", "UPDATE", "SET", "DELETE", "JOIN", "ON", "UNION", "ALL", "ANY", "ESCAPE", "FILTER",
    };

    internal static bool IsDataLiteral(string text) =>
        JsonPath.IsMatch(text) || (FormatDirective.IsMatch(text) && FormatShape.IsMatch(text));

    internal static string? SqlToken(string text)
    {
        if (IsDataLiteral(text)) return null;
        foreach (var phrase in Phrases)
        {
            var match = phrase.Match(text);
            if (match.Success) return match.Value.Length > 40 ? match.Value[..40] : match.Value;
        }
        if (!text.Any(char.IsLower))
        {
            foreach (var word in Regex.Split(text, "[^A-Za-z]+"))
                if (Keywords.Contains(word)) return word;
        }
        return null;
    }

    internal static IReadOnlyList<Literal> Of(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var found = new List<Literal>();
        var covered = new HashSet<SyntaxNode>();
        foreach (var node in root.DescendantNodes())
        {
            if (covered.Contains(node)) continue;
            if (node is InterpolatedStringExpressionSyntax interpolated)
            {
                var sb = new StringBuilder();
                foreach (var part in interpolated.Contents)
                    sb.Append(part is InterpolatedStringTextSyntax text ? text.TextToken.ValueText : "?");
                found.Add(Make(sb.ToString(), node));
                foreach (var inner in node.DescendantNodes()) covered.Add(inner);
            }
            else if (node is BinaryExpressionSyntax add && add.IsKind(SyntaxKind.AddExpression) && Leaves(add) is { } parts)
            {
                found.Add(Make(string.Concat(parts), node));
                foreach (var inner in node.DescendantNodes()) covered.Add(inner);
            }
            else if (node is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                found.Add(Make(literal.Token.ValueText, node));
            }
        }
        return found;
    }

    private static List<string>? Leaves(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax lit when lit.IsKind(SyntaxKind.StringLiteralExpression):
                return [lit.Token.ValueText];
            case ParenthesizedExpressionSyntax paren:
                return Leaves(paren.Expression);
            case BinaryExpressionSyntax add when add.IsKind(SyntaxKind.AddExpression):
                var left = Leaves(add.Left);
                var right = Leaves(add.Right);
                if (left is null || right is null) return null;
                left.AddRange(right);
                return left;
            default:
                return null;
        }
    }

    private static Literal Make(string text, SyntaxNode node) =>
        new(text, node.GetLocation().GetLineSpan().StartLinePosition.Line + 1, node.Ancestors()
            .OfType<ObjectCreationExpressionSyntax>()
            .Any(o => o.Type.ToString().EndsWith("Exception", StringComparison.Ordinal)));

    internal static IEnumerable<string> SourceFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                        && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal);
}
