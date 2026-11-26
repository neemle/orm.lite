namespace Neemle.OrmLite;

internal static class GraphSaveDependencies
{
    internal static IReadOnlyDictionary<GraphSaveNode, HashSet<GraphSaveNode>> Create(GraphSaveSnapshot snapshot,
        IReadOnlyDictionary<GraphSaveNode, GraphSaveNode> canonical)
    {
        var dependencies = canonical.Values.Distinct().Where(n => n.Intent != GraphSaveIntent.Unchanged)
            .ToDictionary(n => n, _ => new HashSet<GraphSaveNode>());
        foreach (var node in snapshot.Nodes)
        {
            if (node.Intent == GraphSaveIntent.Unchanged) continue;
            foreach (var principal in node.Owners.Values) Add(dependencies, canonical[node], canonical[principal]);
            Physical(node, canonical, dependencies);
        }
        return dependencies;
    }

    private static void Physical(GraphSaveNode node, IReadOnlyDictionary<GraphSaveNode, GraphSaveNode> canonical,
        IReadOnlyDictionary<GraphSaveNode, HashSet<GraphSaveNode>> dependencies)
    {
        foreach (var fk in node.Binding.Descriptor.ForeignKeys)
        {
            var value = node.Values[GraphSaveColumns.Physical(node.Binding.Descriptor, fk.LocalColumn)];
            if (value is null) continue;
            var principals = canonical.Values.Distinct().Where(p => p.Binding.Descriptor.Table == fk.Target.Table
                && p.Binding.Descriptor.SoleKey!.Column == fk.Target.Column
                && p.Binding.KeyComparer.Equals(p.Key, value));
            foreach (var principal in principals) Add(dependencies, canonical[node], principal);
        }
    }

    private static void Add(IReadOnlyDictionary<GraphSaveNode, HashSet<GraphSaveNode>> dependencies,
        GraphSaveNode owner, GraphSaveNode principal)
    {
        if (principal.Intent != GraphSaveIntent.Unchanged) dependencies[owner].Add(principal);
    }
}
