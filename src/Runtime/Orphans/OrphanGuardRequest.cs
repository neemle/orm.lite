namespace Neemle.OrmLite;

public sealed record OrphanGuardRequest
{
    public OrphanGuardRequest(EntityDescriptor Parent, EntityDescriptor Child, string OwnerForeignKey,
        object ParentKey, OrphanLimits Limits)
    {
        if (Parent is null || Child is null || string.IsNullOrWhiteSpace(OwnerForeignKey)
            || ParentKey is null || Limits is null)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        if (Parent.SoleKey is not { Nullable: false } key || !OrphanScalarTypes.Value(key, ParentKey))
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        this.Parent = OrphanDescriptorCopy.Copy(Parent);
        this.Child = OrphanDescriptorCopy.Copy(Child);
        this.OwnerForeignKey = OwnerForeignKey;
        this.ParentKey = ParentKey;
        this.Limits = Limits with { };
    }

    public EntityDescriptor Parent { get; }
    public EntityDescriptor Child { get; }
    public string OwnerForeignKey { get; }
    public object ParentKey { get; }
    public OrphanLimits Limits { get; }
}
