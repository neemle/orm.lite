using System.Data.Common;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.Streaming;

internal sealed class StreamProbeProvider(StreamProbe probe) : ISqlProvider
{
    public string Id => PostgresProvider.Instance.Id;
    public CapabilitySet Capabilities => PostgresProvider.Instance.Capabilities;
    public ISerializer CreateSerializer() => PostgresProvider.Instance.CreateSerializer();
    public Task ClearActorAsync(DbConnection connection, CancellationToken ct) =>
        probe.ActorError is { } error ? Task.FromException(error) : Task.CompletedTask;
    public Task StampActorAsync(DbConnection connection, DbActorValue actor, CancellationToken ct) =>
        ClearActorAsync(connection, ct);
}
