using System.Globalization;

namespace Neemle.Orm.Lite;

internal static class ParameterRedactor
{
    internal const string Redacted = "[redacted]";

    private static readonly string[] HashPrefixes = ["$argon2", "$pbkdf2", "$scrypt"];

    private static readonly HashSet<Type> PlainTypes =
    [
        typeof(bool), typeof(char), typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int),
        typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(Guid),
        typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly), typeof(TimeSpan),
    ];

    private const int TokenLength = 32;

    internal static string? Describe(object? value, int maxLength)
    {
        if (value is null or DBNull) return null;
        switch (value)
        {
            case byte[] bytes:
                return "[" + bytes.Length.ToString(CultureInfo.InvariantCulture) + " bytes]";
            case var plain when IsPlain(plain):
                return Cut(Format(plain), maxLength);
            default:
                return Guarded(TextOf(value), maxLength);
        }
    }

    internal static bool IsSecretShaped(string text) =>
        Judged(text) || (text.Contains('\\') && Judged(JsonEscapes.Decode(text)));

    private static bool Judged(string text) =>
        HasSecretShape(text) || SecretLabels.Any(text) || JsonStrings.Any(text, LiteralShaped);

    private static bool LiteralShaped(string literal) =>
        HasSecretShape(literal) || (literal.Contains('\\') && HasSecretShape(JsonEscapes.Decode(literal)));

    private static bool IsPlain(object value) => value is Enum || PlainTypes.Contains(value.GetType());

    private static string TextOf(object value) => value switch
    {
        string text => text,
        char[] chars => new string(chars),
        ArraySegment<char> segment => new string(segment.AsSpan()),
        _ => Format(value),
    };

    private static string Format(object value) =>
        value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture) ?? ""
            : value.ToString() ?? "";

    private static string Guarded(string text, int maxLength) => IsSecretShaped(text) ? Redacted : Cut(text, maxLength);

    private static bool HasSecretShape(string text) =>
        IsJwt(text) || HasAuthScheme(text) || text.Contains("-----BEGIN", StringComparison.Ordinal)
        || IsBcrypt(text) || HasHashPrefix(text) || HasUrlCredentials(text) || IsTokenShaped(text);

    private static bool IsJwt(string text) =>
        text.StartsWith("eyJ", StringComparison.Ordinal) && text.Count(c => c == '.') == 2;

    private static bool HasAuthScheme(string text) =>
        text.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase);

    private static bool HasHashPrefix(string text) =>
        HashPrefixes.Any(prefix => text.StartsWith(prefix, StringComparison.Ordinal));

    private static bool HasUrlCredentials(string text)
    {
        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        return scheme > 0 && text.IndexOf('@', scheme) > scheme;
    }

    private static bool IsBcrypt(string text) =>
        text.Length > 3 && text[0] == '$' && text[1] == '2' && text[2] is 'a' or 'b' or 'y' && text[3] == '$';

    private static bool IsTokenShaped(string text)
    {
        if (text.Length < TokenLength) return false;
        var letter = false;
        var digit = false;
        foreach (var c in text)
        {
            if (char.IsAsciiLetter(c)) letter = true;
            else if (char.IsAsciiDigit(c)) digit = true;
            else if (c is not ('+' or '/' or '=' or '-' or '_')) return false;
        }
        return letter && digit;
    }

    private static string Cut(string text, int maxLength) =>
        text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength), "...");
}
