namespace Neemle.OrmLite;

internal sealed class GraphSaveNodeOrder : IComparer<GraphSaveNode>
{
    internal static GraphSaveNodeOrder Instance { get; } = new();

    public int Compare(GraphSaveNode? left, GraphSaveNode? right)
    {
        if (ReferenceEquals(left, right)) return 0;
        if (left is null) return -1;
        if (right is null) return 1;
        var table = string.CompareOrdinal(left.Binding.Descriptor.Table, right.Binding.Descriptor.Table);
        if (table != 0) return table;
        var type = string.CompareOrdinal(left.Binding.EntityType.AssemblyQualifiedName,
            right.Binding.EntityType.AssemblyQualifiedName);
        return type != 0 ? type : GraphSaveKeyBytes.Encode(left.Key).AsSpan()
            .SequenceCompareTo(GraphSaveKeyBytes.Encode(right.Key));
    }
}
