using System.Text;
using SQLitePCL;
using static Neemle.Orm.Lite.Sqlite.SqliteEngineValues;

namespace Neemle.Orm.Lite.Sqlite;

internal static class SqliteFunctions
{
    internal const string ILike = "orm_ilike";

    internal const string Actor = "orm_actor";

    internal const string Transaction = "orm_tx";

    internal const string TransactionTime = "orm_tx_at";

    internal const int TextUtf8 = 1;

    internal const int Deterministic = 0x800;

    internal const int Innocuous = 0x200000;

    internal static void Register(sqlite3 handle) =>
        SqliteEngineFunctions.Scalar(
            handle,
            ILike,
            3,
            a => CaseFoldedLike(TextOf(a[0]), TextOf(a[1]), TextOf(a[2])));

    internal static object? CaseFoldedLike(string? value, string? pattern, string? escape)
    {
        if (value is null || pattern is null) return null;
        var escapeRune = escape is { Length: > 0 } ? Rune.GetRuneAt(escape, 0) : (Rune?)null;
        return Matches(Folded(value), Compile(pattern, escapeRune)) ? 1L : 0L;
    }

    private static List<Rune> Folded(string text)
    {
        var runes = new List<Rune>(text.Length);
        foreach (var rune in text.EnumerateRunes()) runes.Add(Rune.ToLowerInvariant(rune));
        return runes;
    }

    private enum Kind
    {
        Literal,
        One,
        Many,
    }

    private readonly record struct Token(Kind Kind, Rune Rune);

    private static List<Token> Compile(string pattern, Rune? escape)
    {
        var tokens = new List<Token>();
        var runes = pattern.EnumerateRunes().ToList();
        for (var i = 0; i < runes.Count; i++)
        {
            var rune = runes[i];
            if (escape is { } marker && rune == marker && i + 1 < runes.Count)
            {
                tokens.Add(new Token(Kind.Literal, Rune.ToLowerInvariant(runes[++i])));
                continue;
            }
            tokens.Add(rune.Value switch
            {
                '%' => new Token(Kind.Many, rune),
                '_' => new Token(Kind.One, rune),
                _ => new Token(Kind.Literal, Rune.ToLowerInvariant(rune)),
            });
        }
        return tokens;
    }

    private static bool Matches(List<Rune> text, List<Token> pattern)
    {
        int t = 0, p = 0, star = -1, mark = 0;
        while (t < text.Count)
        {
            if (p < pattern.Count && pattern[p].Kind == Kind.Many)
            {
                star = p++;
                mark = t;
            }
            else if (p < pattern.Count && (pattern[p].Kind == Kind.One || pattern[p].Rune == text[t]))
            {
                p++;
                t++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++mark;
            }
            else
            {
                return false;
            }
        }
        while (p < pattern.Count && pattern[p].Kind == Kind.Many) p++;
        return p == pattern.Count;
    }
}
