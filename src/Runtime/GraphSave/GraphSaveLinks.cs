namespace Neemle.OrmLite;

internal static class GraphSaveLinks
{
    internal static IReadOnlyList<GraphSaveLink> Capture(GraphSaveSnapshot snapshot)
    {
        var links = new HashSet<GraphSaveLink>();
        foreach (var edge in snapshot.Edges)
        {
            if (edge.Binding.Shape.Kind != GraphSaveRelationKind.Link
                || !edge.Rule.Actions.HasFlag(GraphSaveActions.AddLink)) continue;
            var descriptor = GraphSaveRelationChecks.Link(edge.Source.Binding, edge.Target.Binding, edge.Binding);
            var first = (Guid)edge.Source.Key;
            var second = (Guid)edge.Target.Key;
            var ordered = string.CompareOrdinal(descriptor.ThisKey, descriptor.OtherKey) < 0;
            links.Add(ordered
                ? new(descriptor.JoinTable, descriptor.ThisKey, first, descriptor.OtherKey, second)
                : new(descriptor.JoinTable, descriptor.OtherKey, second, descriptor.ThisKey, first));
        }
        return Array.AsReadOnly(links.OrderBy(l => l.Table, StringComparer.Ordinal)
            .ThenBy(l => l.FirstColumn, StringComparer.Ordinal).ThenBy(l => l.FirstKey)
            .ThenBy(l => l.SecondColumn, StringComparer.Ordinal).ThenBy(l => l.SecondKey).ToArray());
    }
}
