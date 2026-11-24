using System.Globalization;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Postgres;

// The one routine through which a name or a value enters the text of a generated PostgreSQL block.
// An identifier is quoted by SqlWriter.Ident, a string literal by SqlWriter.Str, and a name placed
// inside a RAISE message or a format() template has its percent signs doubled. A template takes
// only Escaped parts, so a raw name cannot be spliced into it by mistake.
internal static class PostgresNames
{
    internal readonly struct Escaped(string text)
    {
        internal string Text { get; } = text;
    }

    internal static Escaped Ident(string name) => new(new SqlWriter().Ident(name).ToString());

    internal static Escaped Text(string value) => new(new SqlWriter().Str(value).ToString());

    internal static Escaped Percent(string value) => new(value.Replace("%", "%%", StringComparison.Ordinal));

    internal static Escaped FormatIdent(string name) => Percent(Ident(name).Text);

    internal static Escaped Count(int count) => new(count.ToString(CultureInfo.InvariantCulture));

    internal static Escaped List(IEnumerable<Escaped> items) =>
        new(string.Join(PostgresDdlDataLiterals.ListSeparator, items.Select(i => i.Text)));

    internal static StringLit Regclass(string name) => new(Ident(name).Text);

    internal static Escaped Splice(string template, params Escaped[] parts) =>
        new(string.Format(CultureInfo.InvariantCulture, template, parts.Select(p => (object)p.Text).ToArray()));
}
