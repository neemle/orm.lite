using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Unit.Joins;

// Registers a hand-written model for UnitJoinRow<TMarker> through the public registration API the generator emits.
internal static class UnitJoinModel<TMarker>
{
    internal static readonly EntityDescriptor Descriptor = Create();

    internal static readonly Func<DbDataReader, UnitJoinRow<TMarker>> Map =
        static _ => throw new InvalidOperationException("a join never maps an entity");

    static UnitJoinModel()
    {
        OrmMeta.Register<UnitJoinRow<TMarker>>(Descriptor);
        GraphModel.Register(new GraphEntityBinding<UnitJoinRow<TMarker>>(Descriptor, Map,
            new GraphKeyBinding<UnitJoinRow<TMarker>, Guid>("id", static row => row.Id)));
        JoinModel.RegisterColumn(Descriptor,
            new JoinColumnBinding<UnitJoinRow<TMarker>, Guid>("Id", static (r, o) => r.GetGuid(o)));
        JoinModel.RegisterColumn(Descriptor,
            new JoinColumnBinding<UnitJoinRow<TMarker>, string>("Name", static (r, o) => r.GetString(o)));
        JoinModel.RegisterColumn(Descriptor, new JoinColumnBinding<UnitJoinRow<TMarker>, string?>("Note",
            static (r, o) => r.IsDBNull(o) ? null : r.GetString(o)));
    }

    // A descriptor equal in content but another instance: registering it makes a captured source stale.
    internal static EntityDescriptor Create() =>
        EntityDescriptor.Of(typeof(UnitJoinRow<TMarker>).FullName!, "unit_join", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("Name", "name", ColumnType.Text, false, false)
            .Property("Note", "note", ColumnType.Text, true, false);

    internal static void Restore() => OrmMeta.Register<UnitJoinRow<TMarker>>(Descriptor);
}
