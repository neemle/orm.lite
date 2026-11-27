using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class JoinScalarTypes
{
    internal static ColumnType Kind(Type type)
    {
        type = System.Nullable.GetUnderlyingType(type) ?? type;
        if (type == typeof(string)) return ColumnType.Text;
        if (type == typeof(Guid)) return ColumnType.Uuid;
        if (type == typeof(bool)) return ColumnType.Bool;
        if (type == typeof(int)) return ColumnType.Int32;
        if (type == typeof(long)) return ColumnType.Int64;
        if (type == typeof(DateTime)) return ColumnType.Timestamp;
        throw new JoinReadException(JoinError.UnsupportedType);
    }

    internal static void Binding(Type type, ColumnDescriptor column)
    {
        if (Kind(type) != column.Type) throw new JoinReadException(JoinError.UnsupportedType);
        if (type.IsValueType && (System.Nullable.GetUnderlyingType(type) is not null) != column.Nullable)
            throw new JoinReadException(JoinError.UnsupportedType);
    }

    internal static void Ordered(Type type)
    {
        var kind = Kind(type);
        if (kind is ColumnType.Bool or ColumnType.Uuid) throw new JoinReadException(JoinError.UnsupportedType);
    }

    internal static object? Value(object? value)
    {
        if (value is null) return null;
        _ = Kind(value.GetType());
        if (value is DateTime date && date.Kind != DateTimeKind.Utc)
            throw new JoinReadException(JoinError.UnsupportedType);
        return value;
    }
}
