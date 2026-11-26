namespace Neemle.OrmLite;

internal static class GraphSavePublication
{
    internal static GraphSaveResult Prepare(GraphSavePrepared plan)
    {
        var entries = new Dictionary<object, GraphSaveResultEntry>(ReferenceEqualityComparer.Instance);
        foreach (var node in plan.Snapshot.Nodes)
            entries.Add(node.Entity, new GraphSaveResultEntry(node.Intent != GraphSaveIntent.Unchanged, node.Bound));
        return new GraphSaveResult(entries);
    }
}
