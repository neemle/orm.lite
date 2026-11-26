using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class QueryPolicyReads<T>
{
    internal static async Task<List<T>> RowsAsync(Query<T> query, StatementExecutor executor)
    {
        if (PolicyReadHooks<T>.Rows is { } policy)
            return await policy(query, new RelationReadOptions(), default);
        return await executor.RowsAsync(query.CompileSelect(), query.StreamMap);
    }

    internal static async Task<T?> FirstAsync(Query<T> query, StatementExecutor executor)
    {
        if (PolicyReadHooks<T>.First is { } policy)
            return await policy(query, new RelationReadOptions(), default);
        var snapshot = query.GraphFirstSnapshot();
        var statement = StatementCompiler.Compile(snapshot.Root, snapshot.Database.Provider,
            StatementKind.Read, query.SourceTable);
        var rows = await executor.RowsAsync(statement, snapshot.Map);
        return rows.Count > 0 ? rows[0] : default;
    }

    internal static GraphQueryState<T> FirstSnapshot(GraphQueryState<T> snapshot) =>
        new(snapshot.Database, snapshot.Root with { Limit = new Limit(Math.Min(1, snapshot.Root.Limit?.Count ?? 1)) },
            snapshot.Map)
        {
            Connection = snapshot.Connection,
            Transaction = snapshot.Transaction,
            Masks = snapshot.Masks,
            Unsupported = snapshot.Unsupported,
        };
}
