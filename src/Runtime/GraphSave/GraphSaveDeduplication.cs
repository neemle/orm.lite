namespace Neemle.OrmLite;

internal static class GraphSaveDeduplication
{
    internal static IReadOnlyDictionary<GraphSaveNode, GraphSaveNode> Collapse(GraphSaveSnapshot snapshot)
    {
        var identities = new Dictionary<GraphSaveIdentity, GraphSaveNode>();
        var canonical = new Dictionary<GraphSaveNode, GraphSaveNode>();
        foreach (var node in snapshot.Nodes)
        {
            var identity = new GraphSaveIdentity(node.Binding, node.Key);
            if (!identities.TryGetValue(identity, out var prior))
            {
                identities.Add(identity, node);
                prior = node;
            }
            else if (prior.Intent != node.Intent || !prior.Values.SequenceEqual(node.Values))
                throw GraphSaveRefusal.At(GraphSaveError.DuplicateIdentity);
            canonical.Add(node, prior);
        }
        ValidateOwnership(snapshot.Nodes, canonical);
        return canonical;
    }

    private static void ValidateOwnership(IEnumerable<GraphSaveNode> nodes,
        IReadOnlyDictionary<GraphSaveNode, GraphSaveNode> canonical)
    {
        var owners = new Dictionary<(GraphSaveNode Node, int Ordinal), GraphSaveNode>();
        foreach (var node in nodes)
        {
            foreach (var pair in node.Owners)
            {
                var key = (canonical[node], pair.Key);
                var principal = canonical[pair.Value];
                if (owners.TryGetValue(key, out var prior) && !ReferenceEquals(prior, principal))
                    throw GraphSaveRefusal.At(GraphSaveError.ConflictingForeignKey);
                owners[key] = principal;
            }
        }
    }
}
