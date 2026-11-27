namespace Neemle.OrmLite;

/// <summary>
/// The validated shape of one guard request: the parent, the owned child, and the child's single owner foreign key,
/// which must reference the parent's key with the same type. Only the library builds it.
/// </summary>
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

    /// <summary>The request the shape was validated from.</summary>
    public OrphanGuardRequest Request { get; }
    /// <summary>The parent entity's metadata.</summary>
    public EntityDescriptor Parent => Request.Parent;
    /// <summary>The owned child entity's metadata.</summary>
    public EntityDescriptor Child => Request.Child;
    /// <summary>The child's owner foreign-key column.</summary>
    public ColumnDescriptor Owner { get; }
    internal IOrphanEntity ParentBinding { get; }
    internal IOrphanEntity ChildBinding { get; }

    /// <summary>
    /// Refuses a mode the child mapping cannot honor: <see cref="OrphanMode.Disabled"/> or an undefined value, Nullify
    /// over a NOT NULL owner key, SoftDelete without declared soft delete, and physical Delete of a soft-delete child.
    /// </summary>
    /// <param name="mode">The mode to check.</param>
    /// <exception cref="OrphanException">The mode is refused.</exception>
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
