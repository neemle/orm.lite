namespace Neemle.OrmLite;

/// <summary>
/// What an <see cref="IOrphanSnapshotGuard"/> observes: one parent row, its owned child table, the owner foreign
/// key and the replacement's limits. The constructor validates and copies everything it is given.
/// </summary>
public sealed record OrphanGuardRequest
{
    /// <summary>Validates and copies the request.</summary>
    /// <param name="Parent">The parent entity; its single key must be a non-JSON scalar.</param>
    /// <param name="Child">The owned child entity; its single key must be a non-JSON scalar.</param>
    /// <param name="OwnerForeignKey">The child's owner foreign key, as a property or column name.</param>
    /// <param name="ParentKey">The parent key value, of the parent key's CLR type.</param>
    /// <param name="Limits">The replacement's limits; copied.</param>
    /// <exception cref="OrphanException">A missing or unsupported argument, with
    /// <see cref="OrphanError.InvalidRequest"/> or <see cref="OrphanError.UnsupportedMapping"/>.</exception>
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

    /// <summary>A copy of the parent entity's metadata.</summary>
    public EntityDescriptor Parent { get; }
    /// <summary>A copy of the owned child entity's metadata.</summary>
    public EntityDescriptor Child { get; }
    /// <summary>The child's owner foreign key, as given.</summary>
    public string OwnerForeignKey { get; }
    /// <summary>The key of the parent whose children are observed.</summary>
    public object ParentKey { get; }
    /// <summary>A copy of the replacement's limits.</summary>
    public OrphanLimits Limits { get; }
}
