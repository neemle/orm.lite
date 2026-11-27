namespace Neemle.OrmLite;

public static class OrphanCollections
{
    public static OwnedCollection<TParent, TChild, TParentKey, TChildKey>
        Relation<TParent, TChild, TParentKey, TChildKey>(string navigation)
        where TParent : class where TChild : class where TParentKey : notnull where TChildKey : notnull =>
        OrphanModel.Resolve(typeof(TParent), typeof(TChild), navigation)
            is OwnedCollection<TParent, TChild, TParentKey, TChildKey> typed ? typed
            : throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
}
