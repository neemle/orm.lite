namespace Neemle.OrmLite;

internal static class PolicyPreflight
{
    internal static void Validate(IPolicyOperation operation, IGraphEntityBinding root)
    {
        var seen = new HashSet<Type>();
        var queue = new Queue<IGraphEntityBinding>();
        queue.Enqueue(root);
        while (queue.TryDequeue(out var source))
        {
            if (!seen.Add(source.EntityType)) continue;
            var policy = RelationPolicies.Find(source.EntityType);
            if (policy is null) continue;
            foreach (var edge in policy.Eager)
            {
                operation.Node(source, edge);
                queue.Enqueue(GraphModel.Find(edge.Target));
            }
        }
    }
}
