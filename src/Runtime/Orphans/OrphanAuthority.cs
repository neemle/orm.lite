namespace Neemle.OrmLite;

internal static class OrphanAuthority<TKey> where TKey : notnull
{
    internal static IReadOnlyList<TKey> Copy(OrphanOperation operation, IOrphanGuardLease guard)
    {
        var keys = guard.OriginalKeys ?? throw OrphanRefusal.At(OrphanError.UnsupportedGuard);
        var count = keys.Count;
        if (count < 0 || count > operation.Options.Limits.MaxObserved)
            throw OrphanRefusal.At(OrphanError.LimitExceeded);
        var distinct = new HashSet<TKey>(EqualityComparer<TKey>.Default);
        for (var i = 0; i < count; i++)
        {
            if (keys[i] is not TKey key) throw OrphanRefusal.At(OrphanError.UnsupportedGuard);
            if (!distinct.Add(key)) throw OrphanRefusal.At(OrphanError.DuplicateIdentity);
        }
        operation.Validate();
        return Array.AsReadOnly(distinct.OrderBy(k => k, OrphanKeyOrder<TKey>.Instance).ToArray());
    }
}
