using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Orphans;

// The real SQLite or PostgreSQL provider and its real snapshot guard, with one change: after the guard has observed
// and verified, a statement runs on the scope's own connection and transaction. It stands for a row that changed
// between the authority and the write (the guard's locks make that unreachable from a second writer), so the
// orphan write statements themselves must notice it.
internal sealed class StaleGuardProvider(ISqlProvider inner, string sql, object?[] values)
    : ISqlProvider, IOrphanSnapshotGuard
{
    public string Id => inner.Id;
    public CapabilitySet Capabilities => inner.Capabilities;
    public ISerializer CreateSerializer() => inner.CreateSerializer();
    public Node Lower(Node tree) => inner.Lower(tree);
    public IMigrationPlanner? Planner => inner.Planner;
    public ISchemaCatalog CreateCatalog(DbConnection connection, DbTransaction? transaction = null) =>
        inner.CreateCatalog(connection, transaction);
    public int MaxBindParameters => inner.MaxBindParameters;
    public void ValidateDataSource(string name, DbDataSource source) => inner.ValidateDataSource(name, source);
    public Task StampActorAsync(DbConnection connection, DbActorValue actor, CancellationToken ct) =>
        inner.StampActorAsync(connection, actor, ct);
    public Task ClearActorAsync(DbConnection connection, CancellationToken ct) => inner.ClearActorAsync(connection, ct);
    public ValueTask<ProviderTransaction> BeginTransactionAsync(DbConnection connection, DbDataSource source,
        CancellationToken ct = default) => inner.BeginTransactionAsync(connection, source, ct);
    public ValueTask<IAsyncDisposable?> EnterWriteAsync(DbDataSource source, CancellationToken ct = default) =>
        inner.EnterWriteAsync(source, ct);

    public async Task<IOrphanGuardLease> AcquireAsync(DbConnection connection, DbTransaction transaction,
        OrphanGuardRequest request, CancellationToken ct) =>
        new StaleGuardLease(await ((IOrphanSnapshotGuard)inner).AcquireAsync(connection, transaction, request, ct),
            connection, transaction, sql, values);
}
