namespace Neemle.OrmLite;

internal static class GraphSaveForeignKeys
{
    internal static void Bind(GraphSaveSnapshot snapshot)
    {
        foreach (var edge in snapshot.Edges)
        {
            if (edge.Binding.Shape.Kind == GraphSaveRelationKind.Link) continue;
            var sourceOwns = edge.Binding.Shape.ForeignKeyOwner == GraphSaveForeignKeyOwner.Source;
            var owner = sourceOwns ? edge.Source : edge.Target;
            var principal = sourceOwns ? edge.Target : edge.Source;
            var ordinal = GraphSaveColumns.Find(owner.Binding.Descriptor, edge.Binding.Shape.ForeignKeyProperty!);
            Assign(owner, principal, ordinal, edge.Rule.Keys);
        }
    }

    private static void Assign(GraphSaveNode owner, GraphSaveNode principal, int ordinal, GraphSaveKeyBinding mode)
    {
        if (owner.Owners.TryGetValue(ordinal, out var prior)
            && !new GraphSaveIdentity(prior.Binding, prior.Key).Equals(new(principal.Binding, principal.Key)))
            throw GraphSaveRefusal.At(GraphSaveError.ConflictingForeignKey);
        owner.Owners[ordinal] = principal;
        if (mode == GraphSaveKeyBinding.RequireMatch
            && !principal.Binding.KeyComparer.Equals(owner.Supplied[ordinal], principal.Key))
            throw GraphSaveRefusal.At(GraphSaveError.ConflictingForeignKey);
        if (principal.Binding.KeyComparer.Equals(owner.Values[ordinal], principal.Key)) return;
        if (owner.Intent == GraphSaveIntent.Unchanged)
            throw GraphSaveRefusal.At(GraphSaveError.UnchangedForeignKeyBinding);
        if (mode != GraphSaveKeyBinding.BindInWriteBuffer)
            throw GraphSaveRefusal.At(GraphSaveError.ConflictingForeignKey);
        var column = owner.Binding.Descriptor.Columns[ordinal];
        GraphSaveValues.ValidateValue(column, principal.Key);
        owner.Values[ordinal] = principal.Key;
        owner.Bound[column.Property] = principal.Key;
    }
}
