using Data = global::Neemle.OrmLite.Postgres.PostgresSchemaDataLiterals;
using System.Globalization;

namespace Neemle.OrmLite.Postgres;

internal static class PostgresSchemaTypes
{
    private static readonly HashSet<string> Plain = new(StringComparer.Ordinal)
    {
        "integer", "bigint", "boolean", Data.DoublePrecisionStorage, "text", "uuid", "date",
             Data.TimestampStorage, "jsonb",
    };

    internal static SchemaFact<SchemaStorageType> Parse(string native)
    {
        if (Plain.Contains(native)) return SchemaFact<SchemaStorageType>.Known(new(native, null, null));
        if (native.StartsWith(Data.NumericTypePrefix, StringComparison.Ordinal) && native.EndsWith(')'))
            return Numeric(native[8..^1]);
        return SchemaFact<SchemaStorageType>.Unsupported("native-type-grammar");
    }

    private static SchemaFact<SchemaStorageType> Numeric(string shape)
    {
        var parts = shape.Split(',');
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var precision)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var scale)
            && precision is > 0 and <= 1000 && scale >= 0 && scale <= precision)
            return SchemaFact<SchemaStorageType>.Known(new("numeric", precision, scale));
        return SchemaFact<SchemaStorageType>.Unsupported("native-type-grammar");
    }

    internal static SchemaFact<SchemaDefault> Default(string? expression) => expression switch
    {
        null => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.None)),
        "0" or "0::integer" or "0::bigint" or Data.ZeroDoublePrecision =>
            SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.Zero)),
        "false" => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.False)),
        Data.EmptyTextDefault => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.EmptyText)),
        Data.CurrentTimeDefault or "CURRENT_TIMESTAMP" 
            => SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.CurrentTime)),
        _ => SchemaFact<SchemaDefault>.Unsupported("default-grammar"),
    };

}
