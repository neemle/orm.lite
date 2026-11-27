namespace Neemle.OrmLite;

internal static class JoinPlanChecks
{
    internal static void Sources(JoinQueryPlan plan)
    {
        var sources = plan.Sources;
        if (sources.Count > plan.Limits.MaxSources) throw new JoinReadException(JoinError.LimitExceeded);
        var seen = new HashSet<QueryJoinSnapshot>(ReferenceEqualityComparer.Instance);
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!seen.Add(source)) throw new JoinReadException(JoinError.OutOfScopeSource);
            if (!aliases.Add(source.Alias)) throw new JoinReadException(JoinError.DuplicateAlias);
            Source(source);
            Resources(plan.Root.Input, source.Input);
        }
    }

    internal static void Full(JoinQueryPlan plan)
    {
        JoinPredicateLimits.Validate(plan);
        Sources(plan);
        if (plan.Edges.Count == 0) throw new JoinReadException(JoinError.InvalidRequest);
        var prior = new List<QueryJoinSnapshot> { plan.Root };
        foreach (var edge in plan.Edges)
        {
            Right(edge.Source);
            JoinPredicateScope.Edge(prior, edge.Source, edge.On, plan.Limits.MaxPredicateNodes);
            prior.Add(edge.Source);
        }
        if (plan.EffectiveLimit > plan.Limits.MaxResultRows) throw new JoinReadException(JoinError.LimitExceeded);
        Paging(plan);
    }

    private static void Source(QueryJoinSnapshot source)
    {
        source.Current();
        if (source.Input.Masked) throw new JoinReadException(JoinError.UnsupportedVisibility);
        if (source.Input.Unsupported) throw new JoinReadException(JoinError.UnsupportedSource);
        JoinAliases.Validate(source.Alias, source.Input.Database.Provider, JoinError.InvalidRequest);
        _ = JoinSourceNodes.Copy(source.Root, source.Metadata, source.Columns);
    }

    private static void Right(QueryJoinSnapshot source)
    {
        if (source.Root.OrderBy is not null || source.Root.Offset is not null || source.Root.Limit is not null)
            throw new JoinReadException(JoinError.UnsupportedOperation);
    }

    private static void Paging(JoinQueryPlan plan)
    {
        var capturedOrder = !plan.ReplaceOrder && plan.Root.Root.OrderBy is { Terms.Count: > 0 };
        if ((plan.EffectiveOffset is not null || plan.EffectiveLimit is not null)
            && !capturedOrder && plan.Orders.Count == 0)
            throw new JoinReadException(JoinError.InvalidRequest);
    }

    private static void Resources(JoinInput root, JoinInput source)
    {
        if (!ReferenceEquals(root.Database, source.Database)
            || !ReferenceEquals(root.Database.Provider, source.Database.Provider)
            || !ReferenceEquals(root.Database.DataSource, source.Database.DataSource))
            throw new JoinReadException(JoinError.ResourceMismatch);
        if (source.Borrowed is not { } borrowed) return;
        if (root.Borrowed is not { } owner || !ReferenceEquals(owner.Connection, borrowed.Connection)
            || !ReferenceEquals(owner.Transaction, borrowed.Transaction))
            throw new JoinReadException(JoinError.ResourceMismatch);
    }
}
