namespace Neemle.OrmLite.Tests.Graph;

internal sealed class GraphTypedKeyRow<TKey> where TKey : notnull
{
    public required TKey Id { get; set; }
    public List<GraphTypedKeyRow<TKey>> Peers { get; set; } = [];
}
