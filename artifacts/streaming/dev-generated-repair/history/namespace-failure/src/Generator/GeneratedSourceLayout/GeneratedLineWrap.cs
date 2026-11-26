using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Neemle.OrmLite.Generator;

/// <summary>Wraps only trivia between tokens, retaining unrelated mapper text verbatim.</summary>
internal static class GeneratedLineWrap
{
    private const int Width = 120;

    internal static string Wrap(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        var maps = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == "Map").Select(m => m.FullSpan).ToArray();
        var tokens = root.DescendantTokens().Where(t => t.Span.Length != 0).ToArray();
        var result = new StringBuilder();
        foreach (var line in tree.GetText().Lines)
        {
            var text = line.ToString();
            if (text.Length <= Width || maps.Any(m => m.IntersectsWith(line.Span))) result.Append(text);
            else AppendLine(result, text, tokens.Where(t => line.Span.Contains(t.Span.Start))
                .Select(t => t.Span.Start - line.Start).ToArray());
            result.Append(source.Substring(line.End, line.EndIncludingLineBreak - line.End));
        }
        return result.ToString();
    }

    private static void AppendLine(StringBuilder result, string line, IReadOnlyList<int> starts)
    {
        var indent = new string(' ', line.TakeWhile(char.IsWhiteSpace).Count() + 4);
        var offset = 0;
        var prefix = "";
        while (prefix.Length + line.Length - offset > Width)
        {
            var cut = Cut(starts, offset, Width - prefix.Length);
            if (cut <= offset) break;
            result.Append(prefix).Append(line.Substring(offset, cut - offset).TrimEnd()).Append('\n');
            offset = cut;
            prefix = indent;
        }
        result.Append(prefix).Append(line.Substring(offset));
    }

    private static int Cut(IReadOnlyList<int> starts, int offset, int width)
    {
        var remaining = starts.Where(s => s > offset).ToArray();
        var fitting = remaining.Where(s => s - offset <= width).ToArray();
        return fitting.Length == 0 ? remaining.FirstOrDefault() : fitting.Last();
    }
}
