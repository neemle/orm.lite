using System.Text;

namespace Neemle.Orm.Lite.Sqlite;

/// <summary>
/// The escaping of a history specification marker (version 2). The marker joins the key, soft-delete and tenant
/// columns with ':' and the unversioned columns with ',', so a column named a:b or x,y would split into the wrong
/// parts; a marker that holds such a name escapes '%', ':' and ',' as %25, %3A and %2C.
/// </summary>
internal static class SqliteHistoryEscape
{
    private const char Mark = '%';

    private static readonly (string Code, char Value)[] Codes = [("%25", '%'), ("%3A", ':'), ("%2C", ',')];

    internal static bool Needs(string text) => text.Contains(':') || text.Contains(',');

    internal static string Escape(string text) =>
        text.Replace("%", "%25", StringComparison.Ordinal)
            .Replace(":", "%3A", StringComparison.Ordinal)
            .Replace(",", "%2C", StringComparison.Ordinal);

    internal static string Unescape(string text)
    {
        if (!text.Contains(Mark)) return text;
        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var code = Codes.FirstOrDefault(c => string.CompareOrdinal(text, i, c.Code, 0, c.Code.Length) == 0);
            if (text[i] == Mark && code.Code is not null)
            {
                result.Append(code.Value);
                i += code.Code.Length - 1;
            }
            else
            {
                result.Append(text[i]);
            }
        }
        return result.ToString();
    }
}
