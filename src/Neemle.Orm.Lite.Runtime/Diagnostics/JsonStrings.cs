namespace Neemle.Orm.Lite;

internal static class JsonStrings
{
    internal static bool Any(string text, Predicate<string> test)
    {
        if (!LooksLikeJson(text)) return false;
        var open = text.IndexOf('"');
        while (open >= 0)
        {
            var close = Close(text, open + 1);
            if (test(text.Substring(open + 1, close - open - 1))) return true;
            open = close < text.Length ? text.IndexOf('"', close + 1) : -1;
        }
        return false;
    }

    private static bool LooksLikeJson(string text)
    {
        var first = 0;
        while (first < text.Length && (char.IsWhiteSpace(text[first]) || text[first] == '\uFEFF')) first++;
        return first < text.Length && text[first] is '{' or '[' or '"';
    }

    private static int Close(string text, int from)
    {
        var i = from;
        while (i < text.Length && text[i] != '"') i += text[i] == '\\' ? 2 : 1;
        return Math.Min(i, text.Length);
    }
}
