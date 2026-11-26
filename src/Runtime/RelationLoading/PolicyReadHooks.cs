namespace Neemle.OrmLite;

internal static class PolicyReadHooks<T>
{
    internal static Func<Query<T>, RelationReadOptions, CancellationToken, Task<List<T>>>? Rows;
    internal static Func<Query<T>, RelationReadOptions, CancellationToken, Task<T?>>? First;
    internal static Func<GraphQueryState<T>, RelationReadOptions, CancellationToken, Task<List<T>>>? Snapshot;
}
