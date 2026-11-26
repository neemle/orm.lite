namespace Neemle.OrmLite;

internal static class GraphSaveColumns
{
    internal static int Key(EntityDescriptor descriptor) => Find(descriptor, descriptor.SoleKey!.Property);

    internal static int Find(EntityDescriptor descriptor, string property)
    {
        for (var i = 0; i < descriptor.Columns.Count; i++)
            if (descriptor.Columns[i].Property == property) return i;
        throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
    }

    internal static int Physical(EntityDescriptor descriptor, string column)
    {
        for (var i = 0; i < descriptor.Columns.Count; i++)
            if (descriptor.Columns[i].Column == column) return i;
        throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
    }
}
