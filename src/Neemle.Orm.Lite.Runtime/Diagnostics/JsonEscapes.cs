using System.Globalization;
using System.Text;

namespace Neemle.Orm.Lite;

internal static class JsonEscapes
{
    internal static string Decode(string text)
    {
        var first = text.IndexOf('\\');
        if (first < 0) return text;
        var builder = new StringBuilder(text.Length).Append(text, 0, first);
        for (var i = first; i < text.Length; i++) i = Append(builder, text, i);
        return builder.ToString();
    }

    private static int Append(StringBuilder builder, string text, int at)
    {
        if (text[at] != '\\' || at + 1 >= text.Length) return Literal(builder, text, at);
        if (text[at + 1] == 'u') return AppendUnicode(builder, text, at);
        if (Simple(text[at + 1]) is not { } decoded) return Literal(builder, text, at);
        builder.Append(decoded);
        return at + 1;
    }

    private static int Literal(StringBuilder builder, string text, int at)
    {
        builder.Append(text[at]);
        return at;
    }

    private static int AppendUnicode(StringBuilder builder, string text, int at)
    {
        var digits = at + 5 < text.Length ? text.AsSpan(at + 2, 4) : default;
        if (digits.Length != 4 || !TryHex(digits, out var code)) return Literal(builder, text, at);
        builder.Append((char)code);
        return at + 5;
    }

    private static bool TryHex(ReadOnlySpan<char> digits, out int code) =>
        int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out code);

    private static char? Simple(char escaped) => escaped switch
    {
        '"' => '"',
        '\\' => '\\',
        '/' => '/',
        'b' => '\b',
        'f' => '\f',
        'n' => '\n',
        'r' => '\r',
        't' => '\t',
        _ => null,
    };
}
