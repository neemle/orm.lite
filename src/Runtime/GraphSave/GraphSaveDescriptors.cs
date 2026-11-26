namespace Neemle.OrmLite;

internal static class GraphSaveDescriptors
{
    internal static EntityDescriptor Freeze(EntityDescriptor source)
    {
        if (!source.Managed || source.IsView || source.SoleKey is null || !SafeSoft(source.Soft))
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
        if (source.Columns.Count == 0 || source.Columns.Select(c => c.Column).Distinct().Count() != source.Columns.Count
            || source.Columns.Select(c => c.Property).Distinct().Count() != source.Columns.Count)
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
        var frozen = EntityDescriptor.Of(source.ClrType, source.Table, source.Managed);
        foreach (var c in source.Columns)
            frozen.Property(c.Property, c.Column, c.Type, c.Nullable, c.Key, c.Precision, c.Scale);
        foreach (var foreignKey in source.ForeignKeys) frozen.ForeignKey(foreignKey with { });
        foreach (var link in source.ManyToMany)
            frozen.Link(link.Navigation, link.JoinTable, link.ThisKey, link.OtherKey, link.OtherTable,
                link.OtherKeyColumn, link.OtherSoftColumn, link.Pure);
        if (source.Soft is { } soft) frozen.SoftDelete(soft.Property, soft.Column, soft.Token);
        return frozen;
    }

    internal static bool Same(EntityDescriptor left, EntityDescriptor right) =>
        left.Table == right.Table && left.ClrType == right.ClrType && left.Managed == right.Managed
        && left.IsView == right.IsView && left.Columns.SequenceEqual(right.Columns)
        && left.ManyToMany.SequenceEqual(right.ManyToMany) && left.ForeignKeys.SequenceEqual(right.ForeignKeys)
        && SameSoft(left.Soft, right.Soft);

    private static bool SafeSoft(SoftDeletion? soft) => soft is null || soft.Platform
        && soft.TenantColumn is null && soft.LabelColumn is null && soft.Owners.Count == 0
        && soft.Pinned.Count == 0 && soft.StatusColumns.Count == 0 && soft.Unversioned.Count == 0;

    private static bool SameSoft(SoftDeletion? left, SoftDeletion? right) => left is null ? right is null
        : right is not null && SafeSoft(right) && left.Property == right.Property
            && left.Column == right.Column && left.Token == right.Token;
}
