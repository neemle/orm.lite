using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class GraphVisibility
{
    internal static void Require(GraphEntitySnapshot entity, string column, string path,
        IReadOnlyDictionary<string, Node> masks)
    {
        if (!masks.ContainsKey(column)) return;
        var property = entity.Columns.First(c => c.Column == column).Property;
        throw new GraphVisibilityException(path, property);
    }
}
