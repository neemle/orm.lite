namespace Neemle.OrmLite;

public sealed class OrphanGuardShape
{
    internal OrphanGuardShape(OrphanGuardRequest request)
    {
        Request = new(request.Parent, request.Child, request.OwnerForeignKey, request.ParentKey, request.Limits);
        ParentBinding = OrphanModel.GuardEntity(Parent);
        ChildBinding = OrphanModel.GuardEntity(Child);
        if (Request.ParentKey.GetType() != ParentBinding.KeyType)
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        var owners = Child.Columns.Where(c => c.Column == Request.OwnerForeignKey
            || c.Property == Request.OwnerForeignKey).ToArray();
        if (owners.Length != 1 || owners[0].Key) throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        Owner = owners[0];
        var parentKey = ParentBinding.Metadata.Key;
        if (Owner.Type != parentKey.Type || Owner.Precision != parentKey.Precision || Owner.Scale != parentKey.Scale)
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        var foreign = Child.ForeignKeys.Where(f => f.LocalColumn == Owner.Column).ToArray();
        if (foreign.Length != 1 || foreign[0].Target.Table != Parent.Table
            || foreign[0].Target.Column != parentKey.Column)
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
    }

    public OrphanGuardRequest Request { get; }
    public EntityDescriptor Parent => Request.Parent;
    public EntityDescriptor Child => Request.Child;
    public ColumnDescriptor Owner { get; }
    internal IOrphanEntity ParentBinding { get; }
    internal IOrphanEntity ChildBinding { get; }

    public void Mode(OrphanMode mode)
    {
        if (!Enum.IsDefined(mode) || mode == OrphanMode.Disabled)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        if (mode == OrphanMode.Nullify && !Owner.Nullable) throw OrphanRefusal.At(OrphanError.Nullability);
        if (mode == OrphanMode.SoftDelete && Child.Soft is null)
            throw OrphanRefusal.At(OrphanError.MissingSoftDelete);
        if (mode == OrphanMode.Delete && Child.Soft is not null)
            throw OrphanRefusal.At(OrphanError.ProtectedRow);
    }
}
