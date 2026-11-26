using System.Data.Common;

namespace Neemle.OrmLite;

internal static class PolicyBoundFirstReader<T> where T : class
{
    internal static async Task<T?> ReadAsync(Query<T> query, DbConnection connection,
        RelationReadOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        ct.ThrowIfCancellationRequested();
        var snapshot = query.GraphFirstSnapshot();
        options = options with { };
        var bound = Bind(snapshot, connection, options);
        var rows = await PolicyReader<T>.SnapshotAsync(bound, options, ct);
        return rows.FirstOrDefault();
    }

    private static GraphQueryState<T> Bind(GraphQueryState<T> snapshot, DbConnection connection,
        RelationReadOptions options)
    {
        if (snapshot.Connection is { } explicitConnection && !ReferenceEquals(explicitConnection, connection))
            throw new RelationScopeException(RelationScopeError.DifferentScope);
        var transaction = snapshot.Transaction;
        if (options.Scope is { } scope)
        {
            if (!ReferenceEquals(scope.Binding.Connection, connection))
                throw new RelationScopeException(RelationScopeError.DifferentScope);
            scope.Binding.Match(snapshot);
            if (snapshot.Connection is null) transaction = scope.Binding.Transaction;
        }
        return new GraphQueryState<T>(snapshot.Database, snapshot.Root, snapshot.Map)
        {
            Connection = connection, Transaction = transaction,
            Masks = snapshot.Masks, Unsupported = snapshot.Unsupported,
        };
    }
}
