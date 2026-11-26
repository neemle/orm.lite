namespace Neemle.OrmLite;

internal static class GraphSaveTopological
{
    internal static IReadOnlyList<GraphSaveNode> Order(
        IReadOnlyDictionary<GraphSaveNode, HashSet<GraphSaveNode>> dependencies)
    {
        var pending = dependencies.ToDictionary(p => p.Key, p => new HashSet<GraphSaveNode>(p.Value));
        var ready = new SortedSet<GraphSaveNode>(pending.Where(p => p.Value.Count == 0).Select(p => p.Key),
            GraphSaveNodeOrder.Instance);
        var ordered = new List<GraphSaveNode>();
        while (ready.Count != 0)
        {
            var node = ready.Min!;
            ready.Remove(node);
            pending.Remove(node);
            ordered.Add(node);
            foreach (var pair in pending)
                if (pair.Value.Remove(node) && pair.Value.Count == 0) ready.Add(pair.Key);
        }
        if (pending.Count != 0) throw GraphSaveRefusal.At(GraphSaveError.Cycle);
        return Array.AsReadOnly(ordered.ToArray());
    }
}
