namespace Neemle.OrmLite;

internal sealed class GraphSaveNode
{
    internal GraphSaveNode(object entity, IGraphSaveEntity binding, GraphSaveIntent intent)
    {
        GraphSaveOrigins.RefuseKnownPartial(entity);
        Entity = entity;
        Binding = binding;
        Intent = intent;
        Key = binding.Key(entity);
        Values = binding.Values(entity);
        Supplied = (object?[])Values.Clone();
        var ordinal = GraphSaveColumns.Key(binding.Descriptor);
        if (!binding.KeyComparer.Equals(Key, Values[ordinal]))
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
    }

    internal object Entity { get; }
    internal IGraphSaveEntity Binding { get; }
    internal GraphSaveIntent Intent { get; }
    internal object Key { get; }
    internal object?[] Values { get; }
    internal object?[] Supplied { get; }
    internal Dictionary<string, object> Bound { get; } = new(StringComparer.Ordinal);
    internal Dictionary<int, GraphSaveNode> Owners { get; } = [];
}
