namespace Neemle.OrmLite;

internal static class OrphanPlan<TChild, TKey> where TChild : class where TKey : notnull
{
    internal static OrphanPrepared<TKey> Build(OrphanOperation operation, IReadOnlyList<TKey> original,
        OrphanReplacementRequest<TChild, TKey> request)
    {
        if (request?.DesiredKeys is null || request.PresentWrites is null)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        operation.Validate();
        var desired = Desired(request.DesiredKeys, operation.Options.Limits.MaxDesired);
        var seen = new HashSet<TKey>(original, EqualityComparer<TKey>.Default);
        var writes = Present(operation, request.PresentWrites, desired, seen);
        foreach (var key in desired)
            if (!seen.Contains(key) && !writes.ContainsKey(key))
                throw OrphanRefusal.At(OrphanError.DesiredMembership);
        var removed = original.Where(k => !desired.Contains(k))
            .OrderBy(k => k, OrphanKeyOrder<TKey>.Instance).ToArray();
        if (removed.Length > operation.Options.Limits.MaxCandidates
            || (long)removed.Length + writes.Count > operation.Options.Limits.MaxWrites)
            throw OrphanRefusal.At(OrphanError.LimitExceeded);
        var present = writes.Values.OrderBy(w => w.Key, OrphanKeyOrder<TKey>.Instance).ToArray();
        return new(Array.AsReadOnly(removed), Array.AsReadOnly(present));
    }

    private static HashSet<TKey> Desired(IReadOnlyList<TKey> supplied, int maximum)
    {
        var keys = new HashSet<TKey>(EqualityComparer<TKey>.Default);
        var count = supplied.Count;
        if (count < 0) throw OrphanRefusal.At(OrphanError.InvalidRequest);
        for (var i = 0; i < count; i++)
        {
            var key = supplied[i];
            if (key is null) throw OrphanRefusal.At(OrphanError.InvalidRequest);
            keys.Add(key);
            if (keys.Count > maximum) throw OrphanRefusal.At(OrphanError.LimitExceeded);
        }
        return keys;
    }

    private static Dictionary<TKey, OrphanFrozenWrite<TKey>> Present(OrphanOperation operation,
        OrphanPresentWrites<TChild> supplied, HashSet<TKey> desired, HashSet<TKey> original)
    {
        var writes = new Dictionary<TKey, OrphanFrozenWrite<TKey>>(EqualityComparer<TKey>.Default);
        foreach (var item in supplied.Items)
        {
            var next = OrphanPayloadCapture.Copy<TChild, TKey>(operation, item);
            Membership(next, desired, original);
            if (writes.TryGetValue(next.Key, out var prior)
                && (prior.Intent != next.Intent || !OrphanValueEquality.Same(prior.Values, next.Values)))
                throw OrphanRefusal.At(OrphanError.ConflictingIdentity);
            writes[next.Key] = next;
            if (writes.Count > operation.Options.Limits.MaxWrites) throw OrphanRefusal.At(OrphanError.LimitExceeded);
        }
        return writes;
    }

    private static void Membership(OrphanFrozenWrite<TKey> write, HashSet<TKey> desired, HashSet<TKey> original)
    {
        if (!desired.Contains(write.Key))
            throw OrphanRefusal.At(original.Contains(write.Key)
                ? OrphanError.ConflictingIdentity : OrphanError.DesiredMembership);
        if (write.Intent == OrphanPresentIntent.Insert && original.Contains(write.Key))
            throw OrphanRefusal.At(OrphanError.ConflictingIdentity);
        if (write.Intent == OrphanPresentIntent.Update && !original.Contains(write.Key))
            throw OrphanRefusal.At(OrphanError.DesiredMembership);
    }
}
