namespace Neemle.OrmLite;

internal sealed class JoinQueryPlan
{
    internal JoinQueryPlan(QueryJoinSnapshot root, JoinLimits limits)
    {
        if (root is null || limits is null) throw new JoinReadException(JoinError.InvalidRequest);
        Root = root;
        Limits = limits with { };
    }

    private JoinQueryPlan(JoinQueryPlan prior)
    {
        Root = prior.Root; Limits = prior.Limits; Edges = prior.Edges; Filters = prior.Filters;
        Orders = prior.Orders; ReplaceOrder = prior.ReplaceOrder; Offset = prior.Offset; Limit = prior.Limit;
    }

    internal QueryJoinSnapshot Root { get; }
    internal JoinLimits Limits { get; }
    internal IReadOnlyList<JoinPlanEdge> Edges { get; private init; } = Array.AsReadOnly<JoinPlanEdge>([]);
    internal IReadOnlyList<JoinPredicate> Filters { get; private init; } = Array.AsReadOnly<JoinPredicate>([]);
    internal IReadOnlyList<JoinPlanOrder> Orders { get; private init; } = Array.AsReadOnly<JoinPlanOrder>([]);
    internal bool ReplaceOrder { get; private init; }
    internal int? Offset { get; private init; }
    internal int? Limit { get; private init; }
    internal int? EffectiveOffset => Offset ?? Root.Root.Offset?.Count;
    internal int? EffectiveLimit => Limit ?? Root.Root.Limit?.Count;
    internal IReadOnlyList<QueryJoinSnapshot> Sources =>
        Array.AsReadOnly(new[] { Root }.Concat(Edges.Select(e => e.Source)).ToArray());

    internal static JoinQueryPlan From<T>(JoinSource<T> root, JoinLimits limits) where T : class =>
        root is null ? throw new JoinReadException(JoinError.InvalidRequest) : new(root.Snapshot, limits);

    internal JoinQueryPlan InnerJoin<T>(JoinSource<T> right, JoinPredicate on) where T : class =>
        Append(right, on, false);
    internal JoinQueryPlan LeftJoin<T>(JoinSource<T> right, JoinPredicate on) where T : class =>
        Append(right, on, true);

    private JoinQueryPlan Append<T>(JoinSource<T> right, JoinPredicate on, bool left) where T : class
    {
        if (right is null || on is null) throw new JoinReadException(JoinError.InvalidRequest);
        if (Edges.Count + 2 > Limits.MaxSources) throw new JoinReadException(JoinError.LimitExceeded);
        var next = new JoinQueryPlan(this)
        { Edges = Array.AsReadOnly(Edges.Append(new JoinPlanEdge(right.Snapshot, on, left)).ToArray()) };
        JoinPredicateLimits.Validate(next);
        JoinPlanChecks.Sources(next);
        JoinPredicateScope.Edge(Sources, right.Snapshot, on, Limits.MaxPredicateNodes);
        return next;
    }

    internal JoinQueryPlan Where(JoinPredicate predicate) => predicate is null
        ? throw new JoinReadException(JoinError.InvalidRequest)
        : new(this) { Filters = Array.AsReadOnly(Filters.Append(predicate).ToArray()) };

    internal JoinQueryPlan OrderBy<T>(JoinColumn<T> column, bool descending = false) =>
        WithOrder(column, descending, true);
    internal JoinQueryPlan ThenBy<T>(JoinColumn<T> column, bool descending = false) =>
        WithOrder(column, descending, false);

    private JoinQueryPlan WithOrder<T>(JoinColumn<T> column, bool descending, bool replace)
    {
        if (column is null) throw new JoinReadException(JoinError.InvalidRequest);
        column.Reference.Predicate(true);
        var prior = replace ? Enumerable.Empty<JoinPlanOrder>() : Orders;
        return new(this) { Orders = Array.AsReadOnly(prior.Append(new(column.Reference, descending)).ToArray()),
            ReplaceOrder = replace || ReplaceOrder };
    }

    internal JoinQueryPlan Skip(int count) => count < 0 ? throw new JoinReadException(JoinError.InvalidRequest)
        : new(this) { Offset = count };
    internal JoinQueryPlan Take(int count) => count < 0 ? throw new JoinReadException(JoinError.InvalidRequest)
        : new(this) { Limit = count };
}
