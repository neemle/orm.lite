using System.Data.Common;

namespace Neemle.OrmLite.Tests.GraphSave;

internal static class SaveReadGraphModel
{
    static SaveReadGraphModel()
    {
        SaveProbeModel.Register();
        GraphModel.Register(new GraphEntityBinding<SaveProbeNode>(SaveProbeModel.Nodes, Map,
            new GraphKeyBinding<SaveProbeNode, Guid>("id", n => n.Id),
            new GraphCollectionBinding<SaveProbeNode, SaveProbeNode, Guid>("Children", "parent_id", n => n.Id,
                n => new(n.ParentId.HasValue, n.ParentId.GetValueOrDefault()), (n, children) => n.Children = children)));
    }
    internal static void Register() { }
    internal static GraphPath<SaveProbeNode, SaveProbeNode> Children =>
        GraphPaths.Edge<SaveProbeNode, SaveProbeNode>("Children");
    internal static SaveProbeNode Map(DbDataReader r) => new()
    {
        Id = r.GetGuid(r.GetOrdinal("id")), ParentId = r.IsDBNull(r.GetOrdinal("parent_id"))
            ? null : r.GetGuid(r.GetOrdinal("parent_id")),
        Name = r.IsDBNull(r.GetOrdinal("name")) ? "" : r.GetString(r.GetOrdinal("name")),
    };
}
