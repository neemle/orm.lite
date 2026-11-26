namespace Neemle.OrmLite;

internal sealed class GraphSaveIdentity(IGraphSaveEntity binding, object key) : IEquatable<GraphSaveIdentity>
{
    internal IGraphSaveEntity Binding { get; } = binding;
    internal object Key { get; } = key;

    public bool Equals(GraphSaveIdentity? other) => other is not null
        && Binding.EntityType == other.Binding.EntityType && Binding.Descriptor.Table == other.Binding.Descriptor.Table
        && Binding.KeyComparer.Equals(Key, other.Key);
    public override bool Equals(object? obj) => obj is GraphSaveIdentity other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Binding.EntityType, Binding.Descriptor.Table,
        Binding.KeyComparer.GetHashCode(Key));
}
