using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Postgres;

namespace FoundationProof;

internal sealed class ProbeProvider(Probe probe) : ISqlProvider
{
    public string Id => "proof";
    public CapabilitySet Capabilities => CapabilitySet.Everything();
    public ISerializer CreateSerializer() => PostgresProvider.Instance.CreateSerializer();
    public Task ClearActorAsync(DbConnection connection, CancellationToken ct)
    {
        probe.Actors++;
        return Task.CompletedTask;
    }
    public Task StampActorAsync(DbConnection connection, DbActorValue actor, CancellationToken ct) =>
        ClearActorAsync(connection, ct);
}
