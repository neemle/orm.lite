using System.Data.Common;

namespace Neemle.OrmLite.Ports.Tests.Fake;

// A third provider built from the public ports alone: a serializer, a capability set and an actor stamp. It supplies
// no migration planner, so the runner uses the lock-free default planner of the core. It names no driver and no
// shipped provider, so it can run in a process where only the Runtime and Sql assemblies are loaded. What it cannot
// do (documents, row history, locks) it declares as unsupported.
public sealed class FakeProvider : ISqlProvider
{
    public const string ProviderId = "fake";

    private readonly FakeDatabase _store;

    public FakeProvider(FakeDatabase store)
    {
        _store = store;
    }

    public string Id => ProviderId;

    public CapabilitySet Capabilities { get; } = CapabilitySet.Everything()
        .With(Capability.SessionLock, Support.None)
        .With(Capability.NamedLock, Support.None)
        .With(Capability.JsonDocument, Support.None)
        .With(Capability.RowHistoryTriggers, Support.None)
        .With(Capability.ObjectMarker, Support.None);

    public ISerializer CreateSerializer() => new FakeSerializer();

    public Task StampActorAsync(DbConnection connection, DbActorValue actor, CancellationToken ct)
    {
        _store.Note("stamp:" + actor.Stamp);
        return Task.CompletedTask;
    }
}
