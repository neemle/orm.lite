namespace Neemle.OrmLite;

internal static class SecretLabels
{
    private const int MaxLabel = 64;

    private const int MaxSkip = 16;

    internal static bool Any(string text)
    {
        for (var i = 0; i < text.Length; i++)
            if (Terminator(text, i) is { } opener && Names(text, i, opener)) return true;
        return false;
    }

    private static char? Terminator(string text, int at)
    {
        switch (text[at])
        {
            case ':' or '=':
                return '\0';
            case '>':
                return '<';
            case ']':
                return '[';
            case '%':
                return IsEscapedSeparator(text, at) ? '\0' : null;
            default:
                return null;
        }
    }

    private static bool IsEscapedSeparator(string text, int at) =>
        at + 2 < text.Length && text[at + 1] == '3' && text[at + 2] is 'A' or 'a' or 'D' or 'd';

    private static bool Names(string text, int at, char opener)
    {
        var end = SkipBack(text, at);
        var begin = LabelStart(text, end);
        if (begin >= end) return false;
        begin = WithPrecedingWord(text, begin, end);
        return OpenerBefore(text, begin, opener) && LabelWords.Mentions(text.AsSpan(begin, end - begin));
    }

    private static int SkipBack(string text, int at)
    {
        var end = at;
        while (end > 0 && at - end < MaxSkip && IsSkippable(text[end - 1])) end--;
        return end;
    }

    private static int LabelStart(string text, int end)
    {
        var begin = end;
        while (begin > 0 && end - begin < MaxLabel && IsLabelChar(text[begin - 1])) begin--;
        return begin;
    }

    private static int WithPrecedingWord(string text, int begin, int end)
    {
        if (begin < 2 || text[begin - 1] != ' ' || !IsLabelChar(text[begin - 2])) return begin;
        var start = begin - 1;
        while (start > 0 && end - start < MaxLabel && IsLabelChar(text[start - 1])) start--;
        return start;
    }

    private static bool OpenerBefore(string text, int begin, char opener)
    {
        if (opener == '\0') return true;
        var at = begin - 1;
        if (at >= 0 && text[at] is '"' or '\'') at--;
        return at >= 0 && text[at] == opener;
    }

    private static bool IsSkippable(char c) => char.IsWhiteSpace(c) || c is '"' or '\'' or ']' or ')' or '`';

    private static bool IsLabelChar(char c) => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.';
}
