using System.Data.Common;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveStampProvider(SaveFaultProbe probe) : ISqlProvider
{
    public string Id => "independent-actor-stamp";
    public CapabilitySet Capabilities => PostgresProvider.Instance.Capabilities;
    public ISerializer CreateSerializer() => PostgresProvider.Instance.CreateSerializer();
    public Task ClearActorAsync(DbConnection connection, CancellationToken ct) => Task.FromException(probe.Primary);
    public Task StampActorAsync(DbConnection connection, DbActorValue actor, CancellationToken ct) => Task.FromException(probe.Primary);
}
