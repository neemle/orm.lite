using System.Globalization;
using System.Numerics;

namespace Neemle.Orm.Lite.Sqlite;

internal static class SqliteJsonInput
{
    private const long MaxExponent = int.MaxValue / 2;

    private const int MaxWholeDigits = 131072;

    internal static int Int32(string text)
    {
        var failure = new FormatException($"invalid input syntax for type integer: \"{text}\"");
        var value = ParseInteger(text) ?? throw failure;
        if (value < int.MinValue || value > int.MaxValue)
            throw new OverflowException($"value \"{text}\" is out of range for type integer");
        return (int)value;
    }

    internal static double? Numeric(string text)
    {
        var trimmed = Trim(text);
        if (trimmed.Length == 0) return null;
        if (Special(trimmed, signedNan: false) is { } special) return double.IsNaN(special) ? null : special;
        if (NonDecimal(trimmed) is { } integer) return (double)integer;
        return DecimalValue(trimmed, numeric: true);
    }

    internal static double? Float8(string text)
    {
        var trimmed = Trim(text);
        if (trimmed.Length == 0) return null;
        if (Special(trimmed, signedNan: true) is { } special) return double.IsNaN(special) ? null : special;
        if (HexOnly(trimmed) is { } integer) return (double)integer;
        return DecimalValue(trimmed, numeric: false);
    }

    internal static bool? Boolean(string text)
    {
        var value = Trim(text);
        if (value.Length == 0) return null;
        var lower = value.ToLowerInvariant();
        switch (lower[0])
        {
            case 't':
                return "true".StartsWith(lower, StringComparison.Ordinal) ? true : null;
            case 'f':
                return "false".StartsWith(lower, StringComparison.Ordinal) ? false : null;
            case 'y':
                return "yes".StartsWith(lower, StringComparison.Ordinal) ? true : null;
            case 'n':
                return "no".StartsWith(lower, StringComparison.Ordinal) ? false : null;
            case 'o':
                if (lower == "on") return true;
                return lower is "of" or "off" ? false : null;
            case '1':
                return lower.Length == 1 ? true : null;
            case '0':
                return lower.Length == 1 ? false : null;
            default:
                return null;
        }
    }

    private static bool IsSpace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\v' or '\f';

    private static string Trim(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && IsSpace(text[start])) start++;
        while (end > start && IsSpace(text[end - 1])) end--;
        return text[start..end];
    }

    private static double? Special(string text, bool signedNan)
    {
        var sign = 1.0;
        var rest = text;
        if (rest.Length > 0 && rest[0] is '+' or '-')
        {
            sign = rest[0] == '-' ? -1.0 : 1.0;
            rest = rest[1..];
        }
        if (rest.Equals("nan", StringComparison.OrdinalIgnoreCase) && (signedNan || rest.Length == text.Length)) return double.NaN;
        if (rest.Equals("inf", StringComparison.OrdinalIgnoreCase) || rest.Equals("infinity", StringComparison.OrdinalIgnoreCase))
            return sign * double.PositiveInfinity;
        return null;
    }

    private static BigInteger? ParseInteger(string text)
    {
        var trimmed = Trim(text);
        if (trimmed.Length == 0) return null;
        var negative = false;
        var rest = trimmed;
        if (rest[0] is '+' or '-')
        {
            negative = rest[0] == '-';
            rest = rest[1..];
        }
        var magnitude = NonDecimalMagnitude(rest) ?? DecimalMagnitude(rest);
        return magnitude is { } value ? (negative ? -value : value) : null;
    }

    private static BigInteger? NonDecimal(string trimmed)
    {
        var negative = false;
        var rest = trimmed;
        if (rest.Length > 0 && rest[0] is '+' or '-')
        {
            negative = rest[0] == '-';
            rest = rest[1..];
        }
        var magnitude = NonDecimalMagnitude(rest);
        return magnitude is { } value ? (negative ? -value : value) : null;
    }

    private static BigInteger? HexOnly(string trimmed)
    {
        var negative = false;
        var rest = trimmed;
        if (rest.Length > 0 && rest[0] is '+' or '-')
        {
            negative = rest[0] == '-';
            rest = rest[1..];
        }
        if (rest.Length < 3 || rest[0] != '0' || rest[1] is not ('x' or 'X')) return null;
        for (var i = 2; i < rest.Length; i++)
            if (!char.IsAsciiHexDigit(rest[i])) return null;
        var magnitude = BigInteger.Parse("0" + rest[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        return negative ? -magnitude : magnitude;
    }

    private static BigInteger? NonDecimalMagnitude(string rest)
    {
        if (rest.Length < 2 || rest[0] != '0') return null;
        var radix = rest[1] switch
        {
            'x' or 'X' => 16,
            'o' or 'O' => 8,
            'b' or 'B' => 2,
            _ => 0,
        };
        if (radix == 0) return null;
        var value = BigInteger.Zero;
        var any = false;
        for (var i = 2; i < rest.Length; i++)
        {
            var c = rest[i];
            if (c == '_')
            {
                if (i + 1 >= rest.Length || DigitOf(rest[i + 1], radix) < 0) return null;
                continue;
            }
            var digit = DigitOf(c, radix);
            if (digit < 0) return null;
            value = value * radix + digit;
            any = true;
        }
        return any ? value : null;
    }

    private static int DigitOf(char c, int radix)
    {
        var digit = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };
        return digit < radix ? digit : -1;
    }

    private static BigInteger? DecimalMagnitude(string rest)
    {
        if (rest.Length == 0 || !char.IsAsciiDigit(rest[0])) return null;
        var value = BigInteger.Zero;
        for (var i = 0; i < rest.Length; i++)
        {
            var c = rest[i];
            if (c == '_')
            {
                if (i + 1 >= rest.Length || !char.IsAsciiDigit(rest[i + 1])) return null;
                continue;
            }
            if (!char.IsAsciiDigit(c)) return null;
            value = value * 10 + (c - '0');
        }
        return value;
    }

    private static double? DecimalValue(string trimmed, bool numeric)
    {
        var i = 0;
        var cleaned = new System.Text.StringBuilder();
        if (trimmed[i] is '+' or '-') cleaned.Append(trimmed[i++]);
        var digitsBefore = 0;
        var digitsAfter = 0;
        var seenPoint = false;
        var nonZero = false;
        while (i < trimmed.Length)
        {
            var c = trimmed[i];
            if (char.IsAsciiDigit(c))
            {
                cleaned.Append(c);
                if (c != '0') nonZero = true;
                if (seenPoint) digitsAfter++;
                else digitsBefore++;
                i++;
            }
            else if (c == '.')
            {
                if (seenPoint) return null;
                seenPoint = true;
                cleaned.Append(c);
                i++;
            }
            else if (c == '_' && numeric)
            {
                if (i == 0 || !char.IsAsciiDigit(trimmed[i - 1]) || i + 1 >= trimmed.Length || !char.IsAsciiDigit(trimmed[i + 1])) return null;
                i++;
            }
            else
            {
                break;
            }
        }
        if (digitsBefore + digitsAfter == 0) return null;
        long exponent = 0;
        if (i < trimmed.Length && trimmed[i] is 'e' or 'E')
        {
            var j = i + 1;
            var start = j;
            if (j < trimmed.Length && trimmed[j] is '+' or '-') j++;
            var digitsStart = j;
            while (j < trimmed.Length && (char.IsAsciiDigit(trimmed[j]) || (numeric && trimmed[j] == '_' && j > digitsStart && j + 1 < trimmed.Length && char.IsAsciiDigit(trimmed[j + 1])))) j++;
            if (j == digitsStart) return null;
            if (!long.TryParse(trimmed.Substring(start, j - start).Replace("_", "", StringComparison.Ordinal), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
                return null;
            i = j;
        }
        if (i != trimmed.Length) return null;
        if (numeric && (exponent >= MaxExponent || exponent <= -MaxExponent)) return null;
        if (numeric && digitsBefore + exponent > MaxWholeDigits && nonZero) return null;
        var text = cleaned.ToString() + "e" + exponent.ToString(CultureInfo.InvariantCulture);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return null;
        if (!numeric && nonZero && (double.IsInfinity(value) || value == 0)) return null;
        return value;
    }
}
