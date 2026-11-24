using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Neemle.Orm.Lite.Sqlite;

internal abstract record JsonValueNode;

internal sealed record JsonObjectNode(IReadOnlyList<KeyValuePair<string, JsonValueNode>> Members) : JsonValueNode
{
    internal JsonValueNode? Find(string key)
    {
        foreach (var member in Members)
            if (string.Equals(member.Key, key, StringComparison.Ordinal)) return member.Value;
        return null;
    }
}

internal sealed record JsonArrayNode(IReadOnlyList<JsonValueNode> Items) : JsonValueNode;

internal sealed record JsonStringNode(string Text) : JsonValueNode;

internal sealed record JsonNumberNode(string Text) : JsonValueNode;

internal sealed record JsonBoolNode(bool Value) : JsonValueNode;

internal sealed record JsonNullNode : JsonValueNode
{
    internal static readonly JsonNullNode Instance = new();
}

internal static class SqliteJsonModel
{
    private const int MaxDepth = 1000;

    private const int MaxWholeDigits = 131072;

    private const int MaxScale = 16383;

    internal static JsonValueNode Parse(string text)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaxDepth });
        }
        catch (JsonException error)
        {
            throw new FormatException("invalid input syntax for type json: " + error.Message, error);
        }
        using (document)
        {
            try
            {
                return Convert(document.RootElement);
            }
            catch (InvalidOperationException error)
            {
                throw new FormatException("invalid input syntax for type json: " + error.Message, error);
            }
        }
    }

    private static JsonValueNode Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => ObjectOf(element),
        JsonValueKind.Array => new JsonArrayNode(element.EnumerateArray().Select(Convert).ToList()),
        JsonValueKind.String => new JsonStringNode(Checked(element.GetString())),
        JsonValueKind.Number => new JsonNumberNode(CanonicalNumber(element.GetRawText())),
        JsonValueKind.True => new JsonBoolNode(true),
        JsonValueKind.False => new JsonBoolNode(false),
        _ => JsonNullNode.Instance,
    };

    private static string Checked(string? text)
    {
        if (text is null || text.Contains('\0'))
            throw new FormatException("unsupported Unicode escape sequence: \\u0000 cannot be converted to text.");
        return text;
    }

    private static JsonObjectNode ObjectOf(JsonElement element)
    {
        var unique = new Dictionary<string, JsonValueNode>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject()) unique[Checked(property.Name)] = Convert(property.Value);
        return Sorted(unique);
    }

    internal static JsonObjectNode Sorted(Dictionary<string, JsonValueNode> members)
    {
        var list = members.ToList();
        list.Sort((a, b) => CompareKeys(a.Key, b.Key));
        return new JsonObjectNode(list);
    }

    private static int CompareKeys(string left, string right)
    {
        var x = Encoding.UTF8.GetBytes(left);
        var y = Encoding.UTF8.GetBytes(right);
        return x.Length != y.Length ? x.Length.CompareTo(y.Length) : x.AsSpan().SequenceCompareTo(y);
    }

    internal static string Write(JsonValueNode node)
    {
        var text = new StringBuilder();
        Write(node, text);
        return text.ToString();
    }

    internal static string? Text(JsonValueNode node) => node switch
    {
        JsonNullNode => null,
        JsonStringNode s => s.Text,
        _ => Write(node),
    };

    private static void Write(JsonValueNode node, StringBuilder text)
    {
        switch (node)
        {
            case JsonObjectNode o:
                text.Append('{');
                for (var i = 0; i < o.Members.Count; i++)
                {
                    if (i > 0) text.Append(',').Append(' ');
                    WriteString(o.Members[i].Key, text);
                    text.Append(':').Append(' ');
                    Write(o.Members[i].Value, text);
                }
                text.Append('}');
                break;
            case JsonArrayNode a:
                text.Append('[');
                for (var i = 0; i < a.Items.Count; i++)
                {
                    if (i > 0) text.Append(',').Append(' ');
                    Write(a.Items[i], text);
                }
                text.Append(']');
                break;
            case JsonStringNode s:
                WriteString(s.Text, text);
                break;
            case JsonNumberNode n:
                text.Append(n.Text);
                break;
            case JsonBoolNode b:
                text.Append(b.Value ? "true" : "false");
                break;
            default:
                text.Append("null");
                break;
        }
    }

    private static void WriteString(string value, StringBuilder text)
    {
        text.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    text.Append('\\').Append('"');
                    break;
                case '\\':
                    text.Append('\\').Append('\\');
                    break;
                case '\b':
                    text.Append('\\').Append('b');
                    break;
                case '\f':
                    text.Append('\\').Append('f');
                    break;
                case '\n':
                    text.Append('\\').Append('n');
                    break;
                case '\r':
                    text.Append('\\').Append('r');
                    break;
                case '\t':
                    text.Append('\\').Append('t');
                    break;
                default:
                    if (c < ' ') text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else text.Append(c);
                    break;
            }
        }
        text.Append('"');
    }

    internal static string CanonicalNumber(string raw)
    {
        var i = 0;
        var negative = raw[0] == '-';
        if (negative) i++;
        var wholeStart = i;
        while (i < raw.Length && char.IsAsciiDigit(raw[i])) i++;
        var whole = raw[wholeStart..i];
        var fraction = "";
        if (i < raw.Length && raw[i] == '.')
        {
            var fractionStart = ++i;
            while (i < raw.Length && char.IsAsciiDigit(raw[i])) i++;
            fraction = raw[fractionStart..i];
        }
        long exponent = 0;
        if (i < raw.Length && (raw[i] == 'e' || raw[i] == 'E'))
        {
            if (!long.TryParse(raw.AsSpan(i + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
                throw new FormatException("value overflows numeric format");
        }
        var digits = whole + fraction;
        var point = whole.Length + exponent;
        var scale = Math.Max(0, fraction.Length - exponent);
        if (point > MaxWholeDigits || scale > MaxScale || point < -MaxScale)
            throw new FormatException("value overflows numeric format");
        string integerPart;
        string fractionPart;
        if (point <= 0)
        {
            integerPart = "0";
            fractionPart = new string('0', (int)-point) + digits;
        }
        else if (point >= digits.Length)
        {
            integerPart = digits + new string('0', (int)(point - digits.Length));
            fractionPart = "";
        }
        else
        {
            integerPart = digits[..(int)point];
            fractionPart = digits[(int)point..];
        }
        integerPart = integerPart.TrimStart('0');
        if (integerPart.Length == 0) integerPart = "0";
        if (digits.All(d => d == '0'))
        {
            negative = false;
            fractionPart = new string('0', (int)scale);
        }
        return (negative ? "-" : "") + integerPart + (fractionPart.Length > 0 ? "." + fractionPart : "");
    }
}
