namespace Neemle.OrmLite;

public static class OrphanCollections
{
    public static Task<OrphanReplacement<TChild, TChildKey>> BeginAsync<TParent, TChild, TParentKey, TChildKey>(
        OrmContext context, OwnedCollection<TParent, TChild, TParentKey, TChildKey> relation, TParentKey parentKey,
        OrphanOptions options, CancellationToken ct = default)
        where TParent : class where TChild : class where TParentKey : notnull where TChildKey : notnull =>
        OrphanBegin.Start<TChild, TChildKey>(new(context, relation, parentKey, options), ct);

    public static OwnedCollection<TParent, TChild, TParentKey, TChildKey>
        Relation<TParent, TChild, TParentKey, TChildKey>(string navigation)
        where TParent : class where TChild : class where TParentKey : notnull where TChildKey : notnull =>
        OrphanModel.Resolve(typeof(TParent), typeof(TChild), navigation)
            is OwnedCollection<TParent, TChild, TParentKey, TChildKey> typed ? typed
            : throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
}
