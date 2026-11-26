namespace Neemle.OrmLite;

internal sealed class GraphSaveCapture(GraphSavePlan plan, CancellationToken cancellationToken)
{
    private readonly Dictionary<object, GraphSaveNode> _seen = new(ReferenceEqualityComparer.Instance);
    private readonly Queue<(GraphSaveNode Node, int Depth)> _pending = new();
    private readonly List<GraphSaveEdge> _edges = [];
    private readonly GraphSaveBudget _budget = new(plan.Limits);
    private object _root = null!;
    private GraphSaveIntent _rootIntent;

    internal GraphSaveSnapshot Read(object root, GraphSaveIntent intent)
    {
        _root = root;
        _rootIntent = intent;
        if (plan.Intents.Items.TryGetValue(root, out var extra) && extra != intent)
            throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        Add(root, intent, 0);
        while (_pending.TryDequeue(out var entry)) Expand(entry.Node, entry.Depth);
        if (plan.Intents.Items.Keys.Any(entity => !_seen.ContainsKey(entity)))
            throw GraphSaveRefusal.At(GraphSaveError.UnusedIntent);
        return new GraphSaveSnapshot(Array.AsReadOnly(_seen.Values.ToArray()), Array.AsReadOnly(_edges.ToArray()));
    }

    private GraphSaveNode Add(object entity, GraphSaveIntent intent, int depth)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_seen.TryGetValue(entity, out var prior)) return prior;
        _budget.Node(depth);
        var node = new GraphSaveNode(entity, GraphSaveModel.Resolve(entity.GetType()), intent);
        _seen.Add(entity, node);
        _pending.Enqueue((node, depth));
        return node;
    }

    private void Expand(GraphSaveNode source, int depth)
    {
        foreach (var relation in source.Binding.Relations)
        {
            if (!plan.Permissions.Rules.TryGetValue(relation, out var rule)) continue;
            cancellationToken.ThrowIfCancellationRequested();
            var targetBinding = GraphSaveModel.Resolve(relation.TargetType);
            GraphSaveRelationChecks.Validate(source.Binding, targetBinding, relation);
            foreach (var target in relation.Capture(source.Entity, _budget.Edge))
            {
                var intent = Intent(target);
                Permit(intent, rule.Actions);
                _edges.Add(new GraphSaveEdge(source, Add(target, intent, checked(depth + 1)), relation, rule));
            }
        }
    }

    private GraphSaveIntent Intent(object entity)
    {
        if (ReferenceEquals(entity, _root)) return _rootIntent;
        return plan.Intents.Items.TryGetValue(entity, out var intent)
            ? intent : throw GraphSaveRefusal.At(GraphSaveError.MissingIntent);
    }

    private static void Permit(GraphSaveIntent intent, GraphSaveActions actions)
    {
        var required = intent switch
        {
            GraphSaveIntent.Insert => GraphSaveActions.Insert,
            GraphSaveIntent.Update => GraphSaveActions.Update,
            _ => GraphSaveActions.None,
        };
        if ((actions & required) != required) throw GraphSaveRefusal.At(GraphSaveError.ActionNotAllowed);
    }
}
