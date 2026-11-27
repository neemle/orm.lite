namespace Neemle.OrmLite;

internal static class GraphSaveValues
{
    internal static void Validate(EntityDescriptor descriptor, object?[] values)
    {
        if (values.Length != descriptor.Columns.Count) throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
        for (var i = 0; i < values.Length; i++) ValidateValue(descriptor.Columns[i], values[i]);
    }

    internal static void ValidateValue(ColumnDescriptor column, object? value)
    {
        if (value is null)
        {
            if (!column.Nullable) throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
            return;
        }
        if (!GraphSaveShapes.Accepts(column.Type, value.GetType()))
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
    }
}
