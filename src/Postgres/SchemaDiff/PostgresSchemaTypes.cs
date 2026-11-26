using System.Globalization;
using System.Text.RegularExpressions;

namespace Neemle.OrmLite.Postgres;

internal static partial class PostgresSchemaTypes
{
    private static readonly HashSet<string> Plain = new(StringComparer.Ordinal)
    {
        "integer", "bigint", "boolean", "double precision", "text", "uuid", "date", "timestamp with time zone", "jsonb",
    };

    internal static SchemaFact<SchemaStorageType> Parse(string native)
    {
        if (Plain.Contains(native)) return SchemaFact<SchemaStorageType>.Known(new(native, null, null));
        var match = Numeric().Match(native);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var precision)
            && int.TryParse(match.Groups[2].Value, out var scale) && precision > 0 && scale >= 0 && scale <= precision)
            return SchemaFact<SchemaStorageType>.Known(new("numeric", precision, scale));
        return SchemaFact<SchemaStorageType>.Unsupported("native-type-grammar");
    }

    internal static SchemaFact<SchemaDefault> Default(string? expression) => expression switch
    {
        null => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.None)),
        "0" or "0::integer" or "0::bigint" or "0::double precision" =>
            SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.Zero)),
        "false" => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.False)),
        "''::text" => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.EmptyText)),
        "now()" or "CURRENT_TIMESTAMP" => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.CurrentTime)),
        _ => SchemaFact<SchemaDefault>.Unsupported("default-grammar"),
    };

    [GeneratedRegex(@"\Anumeric\(([1-9][0-9]{0,3}),([0-9]{1,4})\)\z", RegexOptions.CultureInvariant)]
    private static partial Regex Numeric();
}
