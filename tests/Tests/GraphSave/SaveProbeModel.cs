using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.GraphSave;

internal static class SaveProbeModel
{
    internal static readonly EntityDescriptor Nodes = EntityDescriptor.Of("SaveProbeNode", "gs112_probe_node", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("ParentId", "parent_id", ColumnType.Uuid, true, false)
        .Property("Name", "name", ColumnType.Text, false, false)
        .Link("Tags", "gs112_probe_links", "node_id", "tag_id", "gs112_probe_tag", "id", null, true);
    internal static readonly EntityDescriptor Tags = EntityDescriptor.Of("SaveProbeTag", "gs112_probe_tag", true)
        .Property("Id", "id", ColumnType.Uuid, false, true)
        .Property("Name", "name", ColumnType.Text, false, false);

    static SaveProbeModel()
    {
        OrmMeta.Register<SaveProbeNode>(Nodes);
        OrmMeta.Register<SaveProbeTag>(Tags);
        GraphSaveModel.Register(new GraphSaveEntityBinding<SaveProbeNode, Guid>(Nodes,
            static n => [n.Id, n.ParentId, n.Name], static n => n.Id, MapNode,
            [new GraphSaveRelationBinding<SaveProbeNode, SaveProbeNode>(
                new("Children", GraphSaveRelationKind.Collection, GraphSaveForeignKeyOwner.Target, "ParentId"),
                static n => n.ReadChildren()),
             new GraphSaveRelationBinding<SaveProbeNode, SaveProbeTag>(
                new("Tags", GraphSaveRelationKind.Link, GraphSaveForeignKeyOwner.None, null),
                static n => n.ReadTags())]));
        GraphSaveModel.Register(new GraphSaveEntityBinding<SaveProbeTag, Guid>(Tags,
            static t => [t.Id, t.Name], static t => t.Id, MapTag, []));
    }

    internal static void Register() { }
    internal static SaveRelation<SaveProbeNode, SaveProbeNode> Children =>
        SaveRelations.Edge<SaveProbeNode, SaveProbeNode>("Children");
    internal static SaveRelation<SaveProbeNode, SaveProbeTag> TagEdge =>
        SaveRelations.Edge<SaveProbeNode, SaveProbeTag>("Tags");
    internal static GraphSavePermissions ChildInserts => GraphSavePermissions.None.Allow(Children,
        new(GraphSaveActions.Insert, GraphSaveKeyBinding.BindInWriteBuffer));
    internal static GraphSaveLimits Room => new(new(8, 20, 20), new(20, 20, 50));
    internal static SaveProbeNode Node(int id) => new() { Id = Id(id) };
    internal static Guid Id(int id) => new(id, 0, 0, new byte[8]);
    internal static SaveProbeNode MapNode(DbDataReader r) => new()
    {
        Id = r.GetGuid(r.GetOrdinal("id")), ParentId = r.IsDBNull(r.GetOrdinal("parent_id"))
            ? null : r.GetGuid(r.GetOrdinal("parent_id")), Name = r.GetString(r.GetOrdinal("name")),
    };
    internal static SaveProbeTag MapTag(DbDataReader r) => new()
    { Id = r.GetGuid(r.GetOrdinal("id")), Name = r.GetString(r.GetOrdinal("name")) };
}
