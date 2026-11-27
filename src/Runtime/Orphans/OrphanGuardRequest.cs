namespace Neemle.OrmLite;

public sealed record OrphanGuardRequest
{
    public OrphanGuardRequest(EntityDescriptor Parent, EntityDescriptor Child, string OwnerForeignKey,
        object ParentKey, OrphanLimits Limits)
    {
        if (Parent is null || Child is null || string.IsNullOrWhiteSpace(OwnerForeignKey)
            || ParentKey is null || Limits is null)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        var parent = new OrphanMetadata(Parent);
        var child = new OrphanMetadata(Child);
        if (parent.Key.Type == Nodes.ColumnType.Json || child.Key.Type == Nodes.ColumnType.Json
            || !OrphanScalarTypes.Value(parent.Key, ParentKey))
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        this.Parent = parent.Copy();
        this.Child = child.Copy();
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
