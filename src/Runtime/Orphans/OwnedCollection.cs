namespace Neemle.OrmLite;

public sealed class OwnedCollection<TParent, TChild, TParentKey, TChildKey> : IOrphanCollection
    where TParent : class where TChild : class where TParentKey : notnull where TChildKey : notnull
{
    private readonly IOrphanEntity _parent;
    private readonly IOrphanEntity _child;
    private readonly OrphanCollectionShape _shape;
    private readonly ColumnDescriptor _owner;

    internal OwnedCollection(OrphanEntityBinding<TParent, TParentKey> parent,
        OrphanEntityBinding<TChild, TChildKey> child, OrphanCollectionShape shape)
    {
        _parent = parent;
        _child = child;
        _shape = shape with { };
        _owner = OrphanRelationMetadata.Owner(_parent, _child, _shape);
    }

    IOrphanEntity IOrphanCollection.Parent => _parent;
    IOrphanEntity IOrphanCollection.Child => _child;
    OrphanCollectionShape IOrphanCollection.Shape => _shape;
    ColumnDescriptor IOrphanCollection.Owner => _owner;

    void IOrphanCollection.ValidateCurrent()
    {
        _parent.ValidateCurrent();
        _child.ValidateCurrent();
        if (_owner != OrphanRelationMetadata.Owner(_parent, _child, _shape))
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
    }

    bool IOrphanCollection.Equivalent(IOrphanCollection other) =>
        other is OwnedCollection<TParent, TChild, TParentKey, TChildKey> typed
        && _parent.Equivalent(typed._parent) && _child.Equivalent(typed._child) && _shape == typed._shape;
}
