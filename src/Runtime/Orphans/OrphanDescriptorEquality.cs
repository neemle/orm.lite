namespace Neemle.OrmLite;

internal static class OrphanDescriptorEquality
{
    internal static bool Same(EntityDescriptor left, EntityDescriptor right) =>
        left.ClrType == right.ClrType && left.Table == right.Table && left.Managed == right.Managed
        && left.IsView == right.IsView && left.Columns.SequenceEqual(right.Columns)
        && Relations(left, right) && Protection(left, right);

    private static bool Relations(EntityDescriptor left, EntityDescriptor right) =>
        left.ForeignKeys.SequenceEqual(right.ForeignKeys) && left.ManyToMany.SequenceEqual(right.ManyToMany)
        && left.OneToOnes.SequenceEqual(right.OneToOnes)
        && left.Calculated.Select(c => (c.Property, c.Alias, c.SourceColumn))
            .SequenceEqual(right.Calculated.Select(c => (c.Property, c.Alias, c.SourceColumn)));

    private static bool Protection(EntityDescriptor left, EntityDescriptor right) =>
        SameSoft(left.Soft, right.Soft) && left.Indexes.Count == right.Indexes.Count
        && left.Indexes.Zip(right.Indexes).All(p => p.First.Name == p.Second.Name
            && p.First.Unique == p.Second.Unique && p.First.LiveOnly == p.Second.LiveOnly
            && p.First.Terms.SequenceEqual(p.Second.Terms) && p.First.Filter.SequenceEqual(p.Second.Filter));

    private static bool SameSoft(SoftDeletion? left, SoftDeletion? right) => left is null ? right is null
        : right is not null && left.Property == right.Property && left.Column == right.Column
            && left.Platform == right.Platform && left.TenantColumn == right.TenantColumn
            && left.LabelColumn == right.LabelColumn && left.Token == right.Token && SoftLists(left, right);

    private static bool SoftLists(SoftDeletion left, SoftDeletion right) =>
        left.Unversioned.SequenceEqual(right.Unversioned) && left.Owners.SequenceEqual(right.Owners)
        && left.Pinned.SequenceEqual(right.Pinned) && left.StatusColumns.SequenceEqual(right.StatusColumns);
}
