namespace Neemle.OrmLite;

internal sealed class GraphKeyComparer<TKey> : IEqualityComparer<object> where TKey : notnull
{
    public new bool Equals(object? left, object? right) =>
        left is TKey x && right is TKey y && EqualityComparer<TKey>.Default.Equals(x, y);

    public int GetHashCode(object value) => value is TKey key
        ? EqualityComparer<TKey>.Default.GetHashCode(key)
        : throw new GraphRequestException(GraphRequestError.InvalidBinding);
}
