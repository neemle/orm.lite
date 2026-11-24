namespace Neemle.Orm.Lite;

internal static class LabelWords
{
    private static readonly string[] Contained =
    [
        "password", "passwd", "pwd", "passphrase", "secret", "token", "apikey", "accesskey", "privatekey",
        "authorization", "credential", "cookie", "sessionid",
    ];

    private static readonly string[] Exact = ["pass", "pw", "psw", "pin", "otp", "auth", "jwt", "bearer"];

    internal static bool Mentions(ReadOnlySpan<char> label)
    {
        Span<char> folded = stackalloc char[label.Length];
        var lastStart = Fold(label, folded, out var length);
        var whole = folded[..length];
        return ContainsAny(whole) || EqualsAny(whole) || EqualsAny(whole[lastStart..]);
    }

    private static int Fold(ReadOnlySpan<char> label, Span<char> folded, out int length)
    {
        length = 0;
        var lastStart = 0;
        foreach (var c in label)
        {
            if (c is ' ' or '_' or '-' or '.') lastStart = length;
            else folded[length++] = char.ToLowerInvariant(c);
        }
        return lastStart;
    }

    private static bool ContainsAny(ReadOnlySpan<char> folded)
    {
        foreach (var word in Contained)
            if (folded.IndexOf(word, StringComparison.Ordinal) >= 0) return true;
        return false;
    }

    private static bool EqualsAny(ReadOnlySpan<char> folded)
    {
        foreach (var word in Exact)
            if (MemoryExtensions.Equals(folded, word, StringComparison.Ordinal)) return true;
        return false;
    }
}
