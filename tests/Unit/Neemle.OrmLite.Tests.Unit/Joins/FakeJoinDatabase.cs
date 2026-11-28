using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.Unit.Joins;

// A context over the fake driver and two join sources over UnitJoinRow<TMarker>: `root` and `right`.
internal sealed class FakeJoinDatabase<TMarker>
{
    internal readonly FakeJoinProbe Probe = new();
    internal readonly OrmContext Context;

    internal FakeJoinDatabase()
    {
        _ = UnitJoinModel<TMarker>.Descriptor;
        Context = new OrmContext(new FakeJoinSource(Probe), PostgresProvider.Instance);
    }

    internal Query<UnitJoinRow<TMarker>> Query() => new(Context, UnitJoinModel<TMarker>.Map);

    internal JoinSource<UnitJoinRow<TMarker>> Root() => Query().AsJoinSource("root");

    internal JoinSource<UnitJoinRow<TMarker>> Right() => Query().AsJoinSource("right");
}
