namespace Neemle.OrmLite;

internal static class JoinPredicateScope
{
    private static HashSet<QueryJoinSnapshot> Read(JoinPredicate predicate, int maximum)
    {
        var sources = new HashSet<QueryJoinSnapshot>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<JoinPredicate>();
        pending.Push(predicate);
        var count = 0;
        while (pending.TryPop(out var node))
        {
            if (count++ == maximum) throw new JoinReadException(JoinError.LimitExceeded);
            if (node.Column is { } column) sources.Add(column.Source);
            if (node.Other is { } other) sources.Add(other.Source);
            if (node.Left is { } left) pending.Push(left);
            if (node.Right is { } right) pending.Push(right);
        }
        return sources;
    }

    internal static void Edge(IReadOnlyList<QueryJoinSnapshot> prior, QueryJoinSnapshot right,
        JoinPredicate on, int maximum)
    {
        var used = Read(on, maximum);
        if (!used.Contains(right) || !prior.Any(used.Contains))
            throw new JoinReadException(JoinError.OutOfScopeSource);
        foreach (var source in used)
            if (!ReferenceEquals(source, right) && !prior.Any(p => ReferenceEquals(p, source)))
                throw new JoinReadException(JoinError.OutOfScopeSource);
    }
}
