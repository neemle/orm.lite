using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Neemle.OrmLite.Generator;

/// <summary>Wraps only between C# tokens, retaining literal contents and every emitted declaration token.</summary>
internal sealed class ForeignKeyLineLayout
{
    private const int Width = 120;
    private const string Continuation = "        ";
    private readonly StringBuilder _text = new();
    private int _column;

    internal string Format(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var tokens = root.DescendantTokens().ToArray();
        var chunks = ChunkWidths(tokens, source.Length);
        var position = 0;
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            Append(source.Substring(position, token.SpanStart - position));
            var width = chunks[i] <= Width - Continuation.Length ? chunks[i] : token.Text.Length;
            if (_column + width > Width && _column > Continuation.Length)
            {
                while (_text.Length > 0 && _text[_text.Length - 1] == ' ') _text.Length--;
                Append("\n" + Continuation);
            }
            Append(token.Text);
            position = token.Span.End;
        }
        Append(source.Substring(position));
        return _text.ToString();
    }

    private static int[] ChunkWidths(SyntaxToken[] tokens, int end)
    {
        var widths = new int[tokens.Length];
        for (var i = tokens.Length - 1; i >= 0; i--)
        {
            widths[i] = end - tokens[i].SpanStart;
            if (tokens[i].HasLeadingTrivia) end = tokens[i].FullSpan.Start;
        }
        return widths;
    }

    private void Append(string text)
    {
        _text.Append(text);
        var newline = text.LastIndexOf('\n');
        _column = newline < 0 ? _column + text.Length : text.Length - newline - 1;
    }
}
