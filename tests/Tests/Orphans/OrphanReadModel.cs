using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.Orphans;

internal static class OrphanReadModel<TMarker>
{
    internal static OrphanReadRow<TMarker>? Reuse;
    internal static readonly EntityDescriptor Descriptor = EntityDescriptor.Of("OrphanReadRow", "u1_rows", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
        .Property("Name", "name", ColumnType.Text, false, false)
        .Link("Peers", "u1_links", "from_id", "to_id", "u1_rows", "id", null, true);
    internal static readonly OrphanEntityBinding<OrphanReadRow<TMarker>, Guid> Binding =
        new(Descriptor, Values, static row => row.Id);

    internal static void Register(bool orphan = true)
    {
        OrmMeta.Register<OrphanReadRow<TMarker>>(Descriptor);
        GraphModel.Register(new GraphEntityBinding<OrphanReadRow<TMarker>>(Descriptor, Map,
            new GraphKeyBinding<OrphanReadRow<TMarker>, Guid>("id", static row => row.Id),
            new GraphCollectionBinding<OrphanReadRow<TMarker>, OrphanReadRow<TMarker>, Guid>("Children", "parent_id",
                static row => row.Id, static row => new(row.ParentId.HasValue, row.ParentId.GetValueOrDefault()),
                static (row, children) => row.Children = children),
            new GraphReferenceBinding<OrphanReadRow<TMarker>, OrphanReadRow<TMarker>, Guid>("Parent", "parent_id",
                static row => new(row.ParentId.HasValue, row.ParentId.GetValueOrDefault()),
                static (row, parent) => row.Parent = parent)));
        GraphSaveModel.Register(new GraphSaveEntityBinding<OrphanReadRow<TMarker>, Guid>(Descriptor,
            Values, static row => row.Id, Map, []));
        if (orphan) OrphanModel.RegisterEntity(Binding);
    }

    internal static object?[] Values(OrphanReadRow<TMarker> row)
    { row.ValuesRead++; return [row.Id, row.ParentId, row.Name]; }

    internal static OrphanReadRow<TMarker> Map(DbDataReader reader)
    {
        var row = Reuse ?? new OrphanReadRow<TMarker>();
        row.Id = reader.GetGuid(reader.GetOrdinal("id"));
        row.ParentId = reader.IsDBNull(reader.GetOrdinal("parent_id"))
            ? null : reader.GetGuid(reader.GetOrdinal("parent_id"));
        row.Name = reader.IsDBNull(reader.GetOrdinal("name")) ? "" : reader.GetString(reader.GetOrdinal("name"));
        return row;
    }
}
