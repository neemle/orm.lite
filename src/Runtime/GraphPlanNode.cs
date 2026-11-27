using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class GraphPlanNode(IGraphNavigationBinding edge, GraphEntitySnapshot target, string path)
{
    internal IGraphNavigationBinding Edge { get; } = edge;
    internal GraphEntitySnapshot Target { get; } = target;
    internal string Path { get; } = path;
    internal List<GraphPlanNode> Children { get; } = [];
    internal Select Shape { get; set; } = null!;
    internal bool MayBePartial { get; set; }
    internal int ReservedParameters { get; set; }
}
