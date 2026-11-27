namespace Neemle.OrmLite;

public sealed class OrphanObservation<TKey> where TKey : notnull
{
    internal OrphanObservation(IReadOnlyList<TKey> keys, bool authoritative)
    {
        if (keys is null || keys.Any(k => k is null) || !authoritative && keys.Count != 0)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        if (keys.Distinct(EqualityComparer<TKey>.Default).Count() != keys.Count)
            throw OrphanRefusal.At(OrphanError.DuplicateIdentity);
        OriginalKeys = Array.AsReadOnly(keys.ToArray());
        IsAuthoritative = authoritative;
    }

    public IReadOnlyList<TKey> OriginalKeys { get; }
    public bool IsAuthoritative { get; }
}
