using System.Data.Common;
using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests.Joins;

internal static class JoinUnitModel<TMarker>
{
    internal static readonly EntityDescriptor Descriptor = Create();
    internal static readonly Func<DbDataReader, JoinUnitRow<TMarker>> Map = Read;
    internal static int MapperCalls;

    static JoinUnitModel()
    {
        OrmMeta.Register<JoinUnitRow<TMarker>>(Descriptor);
        GraphModel.Register(new GraphEntityBinding<JoinUnitRow<TMarker>>(Descriptor, Map,
            new GraphKeyBinding<JoinUnitRow<TMarker>, Guid>("id", static row => row.Id)));
        Add<Guid>("Id", static (r, o) => r.GetGuid(o));
        Add<string>("Name", static (r, o) => r.GetString(o));
        Add<string?>("Note", static (r, o) => r.IsDBNull(o) ? null : r.GetString(o));
        Add<int>("Rank", static (r, o) => r.GetInt32(o));
        Add<int?>("OptionalRank", static (r, o) => r.IsDBNull(o) ? null : r.GetInt32(o));
        Add<bool>("Enabled", static (r, o) => r.GetBoolean(o));
        Add<DateTime?>("Stamp", static (r, o) => r.IsDBNull(o) ? null : r.GetDateTime(o));
        Add<DateTime?>("DeletedAt", static (r, o) => r.IsDBNull(o) ? null : r.GetDateTime(o));
        Add<string>("Lower", static (r, o) => r.GetString(o));
    }

    private static void Add<TValue>(string property, Func<DbDataReader, int, TValue> read) =>
        JoinModel.RegisterColumn(Descriptor, new JoinColumnBinding<JoinUnitRow<TMarker>, TValue>(property, read));

    internal static EntityDescriptor Create() =>
        EntityDescriptor.Of(typeof(JoinUnitRow<TMarker>).FullName!, "join_unit", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("Name", "name", ColumnType.Text, false, false)
        .Property("Note", "note", ColumnType.Text, true, false)
        .Property("Rank", "rank", ColumnType.Int32, false, false)
        .Property("OptionalRank", "optional_rank", ColumnType.Int32, true, false)
        .Property("Enabled", "enabled", ColumnType.Bool, false, false)
        .Property("Stamp", "stamp", ColumnType.Timestamp, true, false)
        .Property("DeletedAt", "deleted_at", ColumnType.Timestamp, true, false)
        .CalculatedLower("Lower", "lower", "Name").SoftDelete("DeletedAt", "deleted_at", "unit");

    internal static Query<JoinUnitRow<TMarker>> Query(UnreachableDataSource source) =>
        new(TestContext.Of(source, PostgresProvider.Instance), Map);

    private static JoinUnitRow<TMarker> Read(DbDataReader reader)
    { MapperCalls++; throw new InvalidOperationException("capture must not map an entity"); }
}
