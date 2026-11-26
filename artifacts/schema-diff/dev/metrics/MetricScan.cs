using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Metrics;

internal static class MetricScan
{
    internal static object File(string path)
    {
        var text = System.IO.File.ReadAllText(path);
        var root = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Preview)).GetRoot();
        return new
        {
            File = path,
            Widths = text.Split('\n').Select((s, i) => new { Line = i + 1, Width = s.TrimEnd('\r').Length })
                .Where(x => x.Width > 120).ToArray(),
            Types = root.DescendantNodes().Where(CodeCounts.Bound).Where(n => !n.Ancestors().Any(CodeCounts.Bound))
                .Select(n => new { Name = ((TypeDeclarationSyntax)n).Identifier.ValueText, Kind = n.Kind().ToString(),
                    Line = Start(n), End = End(n), CodeLines = CodeCounts.Lines(n),
                    PrimaryParameters = PrimaryParameters(n) }).ToArray(),
            Methods = CodeCounts.Functions(root).Select(Method).ToArray(),
            SyntaxErrors = root.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.Id).ToArray(),
        };
    }

    private static object Method(SyntaxNode node) => new
    {
        Name = CodeCounts.Name(node), Line = Start(node), End = End(node),
        CodeLines = CodeCounts.Lines(node),
        Parameters = CodeCounts.Parameters(node), Nesting = Complexity.Nesting(node),
        Cyclomatic = 1 + Complexity.Decisions(node).Count,
        Decisions = Complexity.Decisions(node).Select(n => new
            { Line = Start(n), Kind = n.Kind().ToString() }).ToArray(),
    };

    private static int PrimaryParameters(SyntaxNode node) => node switch
    {
        RecordDeclarationSyntax record => record.ParameterList?.Parameters.Count ?? 0,
        ClassDeclarationSyntax type => type.ParameterList?.Parameters.Count ?? 0,
        _ => 0,
    };

    private static int Start(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    private static int End(SyntaxNode node) => node.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
}
