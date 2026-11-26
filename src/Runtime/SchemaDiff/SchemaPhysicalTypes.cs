using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaPhysicalTypes
{
    internal static bool Supported(string profile) => profile is "postgres:schema-diff-v1" or "sqlite:schema-diff-v1";

    internal static SchemaStorageType? Target(ColumnDescriptor column, string profile) => profile switch
    {
        "postgres:schema-diff-v1" => Postgres(column),
        "sqlite:schema-diff-v1" => Sqlite(column),
        _ => null,
    };

    private static SchemaStorageType? Postgres(ColumnDescriptor column) => column.Type switch
    {
        ColumnType.Text => new("text", null, null), ColumnType.Uuid => new("uuid", null, null),
        ColumnType.Int32 => new("integer", null, null), ColumnType.Int64 => new("bigint", null, null),
        ColumnType.Bool => new("boolean", null, null), ColumnType.Double => new("double precision", null, null),
        ColumnType.Timestamp => new("timestamp with time zone", null, null), ColumnType.Date => new("date", null, null),
        _ => null,
    };

    private static SchemaStorageType? Sqlite(ColumnDescriptor column) => column.Type switch
    {
        ColumnType.Text or ColumnType.Uuid or ColumnType.Timestamp or ColumnType.Date => new("text", null, null),
        ColumnType.Int32 or ColumnType.Int64 or ColumnType.Bool => new("integer", null, null),
        ColumnType.Double => new("real", null, null),
        _ => null,
    };

    internal static SchemaDefaultKind Default(ColumnDescriptor column)
    {
        if (column.Key || column.Nullable) return SchemaDefaultKind.None;
        return column.Type switch
        {
            ColumnType.Text => SchemaDefaultKind.EmptyText,
            ColumnType.Bool => SchemaDefaultKind.False,
            ColumnType.Int32 or ColumnType.Int64 or ColumnType.Double => SchemaDefaultKind.Zero,
            ColumnType.Timestamp => SchemaDefaultKind.CurrentTime,
            _ => SchemaDefaultKind.None,
        };
    }
}
