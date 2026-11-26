using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Security.Cryptography;

internal static class WidthLayout
{
    internal static void Run(string list)
    {
        foreach (var file in File.ReadAllLines(list).Where(Owned)) Rewrite(file);
    }

    private static bool Owned(string path) => path.Contains("/SchemaDiff/", StringComparison.Ordinal)
        || path.StartsWith("artifacts/schema-diff/dev/", StringComparison.Ordinal);

    private static void Rewrite(string file)
    {
        var original = File.ReadAllText(file); var source = original;
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var text = Microsoft.CodeAnalysis.Text.SourceText.From(source);
            var wide = text.Lines.Where(line => line.Span.Length > 120).ToArray();
            if (wide.Length == 0) break;
            var longLine = wide[0];
            var root = CSharpSyntaxTree.ParseText(source).GetRoot();
            var position = Boundary(root, longLine.Start, longLine.ToString());
            source = source.Insert(position, "\n" + new string(' ', 12));
        }
        var before = CSharpSyntaxTree.ParseText(original).GetRoot().DescendantTokens().Select(t => (t.RawKind, t.Text));
        var after = CSharpSyntaxTree.ParseText(source).GetRoot().DescendantTokens().Select(t => (t.RawKind, t.Text));
        if (!before.SequenceEqual(after)) throw new InvalidOperationException("token drift " + file);
        if (source == original) return;
        File.WriteAllText(file, source);
        Console.WriteLine(file + " whitespace-only " + Convert.ToHexStringLower(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(source))));
    }

    private static int Boundary(SyntaxNode root, int start, string line)
    {
        var indent = line.Length - line.TrimStart().Length;
        var candidates = root.DescendantTokens().Where(t => t.SpanStart > start + indent + 4
            && t.Span.End < start + 105).Where(t => t.Text is "," or ";" or "&&" or "||" or "=>");
        var token = candidates.LastOrDefault();
        if (token.RawKind == 0)
            return root.DescendantTokens().Where(t => t.Span.End > start + indent + 4
                && t.Span.End < start + 105).Last().Span.End;
        return token.Text is "," or ";" ? token.Span.End : token.SpanStart;
    }
}
