namespace Neemle.OrmLite;

internal static class OrphanRelationMetadata
{
    internal static ColumnDescriptor Owner(IOrphanEntity parent, IOrphanEntity child, OrphanCollectionShape shape)
    {
        if (shape is null || string.IsNullOrWhiteSpace(shape.Navigation)
            || string.IsNullOrWhiteSpace(shape.OwnerForeignKey))
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        var owner = child.Metadata.Columns.Where(c => c.Property == shape.OwnerForeignKey
            || c.Column == shape.OwnerForeignKey).Take(2).ToArray();
        if (owner.Length != 1 || owner[0].Key || !SameKeyShape(parent.Metadata.Key, owner[0]))
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        ValidateForeignKey(parent.Metadata.Copy(), child.Metadata.Copy(), shape, owner[0]);
        return owner[0];
    }

    private static bool SameKeyShape(ColumnDescriptor key, ColumnDescriptor owner) =>
        key.Type == owner.Type && key.Precision == owner.Precision && key.Scale == owner.Scale;

    private static void ValidateForeignKey(EntityDescriptor parent, EntityDescriptor child,
        OrphanCollectionShape shape, ColumnDescriptor owner)
    {
        if (parent.ManyToMany.Any(l => l.Navigation == shape.Navigation))
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        var foreign = child.ForeignKeys.Where(f => f.LocalColumn == owner.Column).ToArray();
        if (foreign.Length != 1 || foreign[0].Target.Table != parent.Table
            || foreign[0].Target.Column != parent.SoleKey!.Column)
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
    }
}
