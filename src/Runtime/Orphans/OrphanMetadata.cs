namespace Neemle.OrmLite;

internal sealed class OrphanMetadata
{
    private readonly EntityDescriptor _original;
    private readonly EntityDescriptor _frozen;

    internal OrphanMetadata(EntityDescriptor descriptor)
    {
        if (descriptor is null || !descriptor.Managed || descriptor.IsView
            || descriptor.SoleKey is not { Nullable: false })
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        if (descriptor.Columns.Count == 0
            || descriptor.Columns.Select(c => c.Column).Distinct(StringComparer.Ordinal).Count()
                != descriptor.Columns.Count
            || descriptor.Columns.Select(c => c.Property).Distinct(StringComparer.Ordinal).Count()
                != descriptor.Columns.Count)
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        if (descriptor.Soft is { } soft && (!soft.Platform || soft.TenantColumn is not null || soft.Owners.Count != 0))
            throw OrphanRefusal.At(OrphanError.UnsupportedVisibility);
        _original = descriptor;
        _frozen = OrphanDescriptorCopy.Copy(descriptor);
    }

    internal EntityDescriptor Copy() => OrphanDescriptorCopy.Copy(_frozen);
    internal IReadOnlyList<ColumnDescriptor> Columns => _frozen.Columns;
    internal ColumnDescriptor Key => _frozen.SoleKey!;
    internal bool Equivalent(OrphanMetadata other) => ReferenceEquals(_original, other._original)
        && OrphanDescriptorEquality.Same(_frozen, other._frozen);

    internal void ValidateCurrent(Type type)
    {
        if (!ReferenceEquals(OrmMeta.ReadDescriptor(type), _original)
            || !OrphanDescriptorEquality.Same(_frozen, _original))
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
    }
}
