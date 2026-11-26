using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class GraphSaveShapes
{
    internal static void Relation(GraphSaveRelationShape shape)
    {
        if (string.IsNullOrWhiteSpace(shape.Navigation) || !Enum.IsDefined(shape.Kind)
            || !Enum.IsDefined(shape.ForeignKeyOwner)) throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
        var expected = shape.Kind switch
        {
            GraphSaveRelationKind.Reference => GraphSaveForeignKeyOwner.Source,
            GraphSaveRelationKind.Collection => GraphSaveForeignKeyOwner.Target,
            _ => GraphSaveForeignKeyOwner.None,
        };
        if (shape.ForeignKeyOwner != expected || (expected == GraphSaveForeignKeyOwner.None
                ? shape.ForeignKeyProperty is not null : string.IsNullOrWhiteSpace(shape.ForeignKeyProperty)))
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
    }

    internal static void Key(Type type, ColumnDescriptor key)
    {
        if (key.Nullable || !Accepts(key.Type, type) || key.Type == ColumnType.Json)
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
    }

    internal static bool Accepts(ColumnType column, Type type) => column switch
    {
        ColumnType.Uuid => type == typeof(Guid),
        ColumnType.Text => type == typeof(string) || type.IsEnum,
        ColumnType.Int32 => type == typeof(int),
        ColumnType.Int64 => type == typeof(long),
        ColumnType.Bool => type == typeof(bool),
        _ => Other(column, type),
    };

    private static bool Other(ColumnType column, Type type) => column switch
    {
        ColumnType.Double => type == typeof(double),
        ColumnType.Decimal => type == typeof(decimal),
        ColumnType.Timestamp => type == typeof(DateTime),
        ColumnType.Date => type == typeof(DateOnly),
        ColumnType.Json => type == typeof(Jsonb),
        _ => false,
    };
}
