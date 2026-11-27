using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class OrphanScalarTypes
{
    internal static bool Supports<TKey>(ColumnType kind) where TKey : notnull => kind switch
    {
        ColumnType.Uuid => typeof(TKey) == typeof(Guid),
        ColumnType.Text => typeof(TKey) == typeof(string) || typeof(TKey).IsEnum,
        ColumnType.Int32 => typeof(TKey) == typeof(int),
        ColumnType.Int64 => typeof(TKey) == typeof(long),
        ColumnType.Bool => typeof(TKey) == typeof(bool),
        ColumnType.Double => typeof(TKey) == typeof(double),
        ColumnType.Decimal => typeof(TKey) == typeof(decimal),
        ColumnType.Timestamp => typeof(TKey) == typeof(DateTime),
        ColumnType.Date => typeof(TKey) == typeof(DateOnly),
        _ => false,
    };

    internal static bool Value(ColumnDescriptor column, object? value) => value is null ? column.Nullable
        : column.Type switch
        {
            ColumnType.Uuid => value is Guid, ColumnType.Text => value is string or Enum,
            ColumnType.Int32 => value is int, ColumnType.Int64 => value is long,
            ColumnType.Bool => value is bool, ColumnType.Double => value is double,
            ColumnType.Decimal => value is decimal,
            _ => TemporalOrJson(column.Type, value),
        };

    private static bool TemporalOrJson(ColumnType kind, object value) => kind switch
    {
        ColumnType.Timestamp => value is DateTime, ColumnType.Date => value is DateOnly,
        ColumnType.Json => value is Jsonb, _ => false,
    };

    internal static bool Immutable(object? value) => value is null or string or Guid or int or long or bool
        or double or decimal or DateTime or DateOnly or Enum or Jsonb;
}
