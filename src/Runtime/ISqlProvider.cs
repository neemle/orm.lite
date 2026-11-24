using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

public readonly record struct ProviderTransaction(DbTransaction Transaction, IAsyncDisposable? Release);

public interface ISqlProvider
{
    string Id { get; }

    CapabilitySet Capabilities { get; }

    ISerializer CreateSerializer();

    Node Lower(Node tree) => tree;

    IMigrationPlanner? Planner => null;

    ISchemaCatalog CreateCatalog(DbConnection connection, DbTransaction? transaction = null) =>
        throw new NotSupportedException($"provider '{Id}' offers no schema catalog.");

    int MaxBindParameters => 32766;

    void ValidateDataSource(string name, DbDataSource source)
    {
    }

    Task StampActorAsync(DbConnection connection, DbActorValue actor, CancellationToken ct) =>
        throw new NotSupportedException($"provider '{Id}' does not stamp the acting user on a connection.");

    Task ClearActorAsync(DbConnection connection, CancellationToken ct) => Task.CompletedTask;

    async ValueTask<ProviderTransaction> BeginTransactionAsync(
        DbConnection connection, DbDataSource source, CancellationToken ct = default) =>
        new(await connection.BeginTransactionAsync(ct), null);

    ValueTask<IAsyncDisposable?> EnterWriteAsync(DbDataSource source, CancellationToken ct = default) =>
        ValueTask.FromResult<IAsyncDisposable?>(null);
}
