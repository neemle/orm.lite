using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.RelationLoadingOwnerCleanup;

internal static class OwnerModel
{
    internal static readonly GraphReferenceBinding<OwnerRow, OwnerRow, int> Edge =
        new("Next", "next_id", row => new(true, row.NextId), (row, value) => row.Next = value);
    private static readonly EntityDescriptor Descriptor = EntityDescriptor.Of("OwnerRow", "owner_rows", false)
        .Property("Id", "id", ColumnType.Int32, false, true)
        .Property("NextId", "next_id", ColumnType.Int32, false, false);
    internal static readonly GraphEntityBinding<OwnerRow> Binding = new(Descriptor, Map,
        new GraphKeyBinding<OwnerRow, int>("id", row => row.Id), Edge);
    internal static readonly GraphLimits Limits = new(new(4, 4, 8), new(4, 4, 4));
    internal static LoadingScopeOptions Options => new(Limits, 8, TimeSpan.FromMinutes(1));
    internal static GraphPath<OwnerRow, OwnerRow> Path => GraphPaths.Edge<OwnerRow, OwnerRow>("Next");
    static OwnerModel() { OrmMeta.Register<OwnerRow>(Descriptor); GraphModel.Register(Binding); }
    private static OwnerRow Map(DbDataReader reader) => throw new NotSupportedException("No row execution allowed");
    internal static PolicySession<OwnerRow> Session(OrmContext context) => new(new GraphQueryState<OwnerRow>(context,
        new Select([new ColumnRef("id"), new ColumnRef("next_id")], new TableRef("owner_rows")), Map), Limits);
}
