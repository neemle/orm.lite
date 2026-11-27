namespace Neemle.OrmLite;

internal static class OrphanDescriptorCopy
{
    internal static EntityDescriptor Copy(EntityDescriptor source)
    {
        var copy = EntityDescriptor.Of(source.ClrType, source.Table, source.Managed);
        if (source.IsView) copy.View();
        foreach (var c in source.Columns)
            copy.Property(c.Property, c.Column, c.Type, c.Nullable, c.Key, c.Precision, c.Scale);
        foreach (var c in source.Calculated)
            copy.CalculatedLower(c.Property, c.Alias, source.Columns.First(s => s.Column == c.SourceColumn).Property);
        CopyIndexes(source, copy);
        foreach (var foreign in source.ForeignKeys) copy.ForeignKey(foreign with { });
        foreach (var link in source.ManyToMany)
            copy.Link(link.Navigation, link.JoinTable, link.ThisKey, link.OtherKey, link.OtherTable,
                link.OtherKeyColumn, link.OtherSoftColumn, link.Pure);
        foreach (var relation in source.OneToOnes) copy.OneToOne(relation with { });
        if (source.Soft is { } soft) CopySoft(copy, soft);
        return copy;
    }

    private static void CopyIndexes(EntityDescriptor source, EntityDescriptor target)
    {
        foreach (var index in source.Indexes)
        {
            if (index.Filter.Any(f => !OrphanScalarTypes.Immutable(f.Value)))
                throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
            target.Index(index.Name, index.Unique, index.LiveOnly, index.Terms.ToArray());
            if (index.Filter.Count != 0) target.Filter(index.Filter.ToArray());
        }
    }

    private static void CopySoft(EntityDescriptor target, SoftDeletion soft)
    {
        target.SoftDelete(soft.Property, soft.Column, soft.Token);
        if (!soft.Platform)
        {
            if (soft.TenantColumn is not { } tenant) throw OrphanRefusal.At(OrphanError.UnsupportedVisibility);
            target.Tenant(tenant);
        }
        if (soft.LabelColumn is { } label) target.Label(label);
        target.Unversioned(soft.Unversioned.ToArray());
        target.Owners(soft.Owners.ToArray());
        target.Pinned(soft.Pinned.ToArray());
        target.StatusColumns(soft.StatusColumns.ToArray());
    }
}
