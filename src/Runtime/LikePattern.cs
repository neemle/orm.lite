#nullable enable
using System.Text;

namespace Neemle.OrmLite
{
    public static class LikePattern
    {
        public const string Escape = "~";

        public const char EscapeChar = '~';

        public const string EscapeClause = " ESCAPE '" + Escape + "'";

        public static string EscapeLiteral(string value)
        {
            if (value.Length == 0) return value;
            var needed = false;
            foreach (var c in value)
                if (c is EscapeChar or '%' or '_') { needed = true; break; }
            if (!needed) return value;

            var sb = new StringBuilder(value.Length + 8);
            foreach (var c in value)
            {
                if (c is EscapeChar or '%' or '_') sb.Append(EscapeChar);
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Contains(string value) => "%" + EscapeLiteral(value) + "%";

        public static string StartsWith(string value) => EscapeLiteral(value) + "%";

        public static string EndsWith(string value) => "%" + EscapeLiteral(value);
    }
}
