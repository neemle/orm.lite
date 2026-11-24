using System.Data.Common;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Tests;

// A provider that answers every question as the provider it wraps does, except for how many bind parameters one
// statement may carry; the chunking and refusal tests pick that limit instead of waiting for 65535 rows.
public sealed class BindLimitProvider(ISqlProvider inner, int maxBindParameters) : ISqlProvider
{
    public string Id => inner.Id;

    public CapabilitySet Capabilities => inner.Capabilities;

    public int MaxBindParameters => maxBindParameters;

    public IMigrationPlanner? Planner => inner.Planner;

    public ISerializer CreateSerializer() => inner.CreateSerializer();

    public Node Lower(Node tree) => inner.Lower(tree);

    public ISchemaCatalog CreateCatalog(DbConnection connection, DbTransaction? transaction = null) =>
        inner.CreateCatalog(connection, transaction);

    public void ValidateDataSource(string name, DbDataSource source) => inner.ValidateDataSource(name, source);

    public Task StampActorAsync(DbConnection connection, DbActorValue actor, CancellationToken ct) =>
        inner.StampActorAsync(connection, actor, ct);

    public Task ClearActorAsync(DbConnection connection, CancellationToken ct) =>
        inner.ClearActorAsync(connection, ct);

    public ValueTask<ProviderTransaction> BeginTransactionAsync(
        DbConnection connection, DbDataSource source, CancellationToken ct = default) =>
        inner.BeginTransactionAsync(connection, source, ct);

    public ValueTask<IAsyncDisposable?> EnterWriteAsync(DbDataSource source, CancellationToken ct = default) =>
        inner.EnterWriteAsync(source, ct);
}
