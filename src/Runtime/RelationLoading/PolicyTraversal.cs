namespace Neemle.OrmLite;

internal sealed class PolicyTraversal(PolicyWork work)
{
    private readonly Dictionary<object, HashSet<IGraphNavigationBinding>> _seen =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _paths = [];
    private readonly Queue<PolicyFrontier> _frontiers = new();

    internal async Task EagerAsync(IGraphEntityBinding binding, IReadOnlyList<object> parents)
    {
        _frontiers.Enqueue(new PolicyFrontier(binding, parents, 0, ""));
        while (_frontiers.TryDequeue(out var frontier))
        {
            var model = RelationPolicies.Find(frontier.Binding.EntityType);
            if (model is null) continue;
            foreach (var edge in model.Eager) await EdgeAsync(frontier, edge);
        }
    }

    internal async Task EdgeAsync(PolicyFrontier frontier, IGraphNavigationBinding edge)
    {
        var parents = frontier.Parents.Where(parent => Visit(parent, edge)).ToList();
        if (parents.Count == 0) return;
        var path = frontier.Path + "/" + edge.Navigation;
        if (frontier.Depth >= work.Operation.Limits.MaxDepth)
            throw new GraphLimitExceededException(GraphBudget.Depth, work.Operation.Limits.MaxDepth);
        if (_paths.Add(path) && _paths.Count > work.Operation.Limits.MaxPaths)
            throw new GraphLimitExceededException(GraphBudget.Paths, work.Operation.Limits.MaxPaths);
        var node = work.Operation.Node(frontier.Binding, edge);
        var rows = await edge.LoadAsync(work.Context, node, parents);
        work.Track(rows, node.Target.Binding);
        if (rows.Count != 0) _frontiers.Enqueue(new PolicyFrontier(node.Target.Binding, rows, frontier.Depth + 1, path))
            ;
    }

    private bool Visit(object parent, IGraphNavigationBinding edge)
    {
        if (!_seen.TryGetValue(parent, out var edges)) _seen.Add(parent, edges = []);
        return edges.Add(edge);
    }

    internal async Task DemandAsync(PolicyDemandWork demand)
    {
        await EdgeAsync(new PolicyFrontier(demand.Attachment.Binding, [demand.Entity], 0, ""), demand.Edge);
        while (_frontiers.TryDequeue(out var frontier))
        {
            var model = RelationPolicies.Find(frontier.Binding.EntityType);
            if (model is null) continue;
            foreach (var edge in model.Eager) await EdgeAsync(frontier, edge);
        }
    }
}
