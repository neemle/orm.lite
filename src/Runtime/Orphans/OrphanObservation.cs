namespace Neemle.OrmLite;

public sealed class OrphanObservation<TKey> where TKey : notnull
{
    internal OrphanObservation(IReadOnlyList<TKey> keys, bool authoritative)
    {
        if (keys is null) throw OrphanRefusal.At(OrphanError.InvalidRequest);
        var copied = keys.ToArray();
        if (copied.Any(k => k is null) || !authoritative && copied.Length != 0)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        if (copied.Distinct(EqualityComparer<TKey>.Default).Count() != copied.Length)
            throw OrphanRefusal.At(OrphanError.DuplicateIdentity);
        OriginalKeys = Array.AsReadOnly(copied);
        IsAuthoritative = authoritative;
    }

    public IReadOnlyList<TKey> OriginalKeys { get; }
    public bool IsAuthoritative { get; }
}
