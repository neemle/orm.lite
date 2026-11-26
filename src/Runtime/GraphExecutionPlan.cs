namespace Neemle.OrmLite;

internal sealed class GraphExecutionPlan<T>(GraphQueryState<T> query, GraphLimits limits,
    GraphEntitySnapshot root) where T : class
{
    internal GraphQueryState<T> Query { get; } = query;
    internal GraphLimits Limits { get; } = limits;
    internal GraphEntitySnapshot Root { get; } = root;
    internal int NodeCount { get; set; }
    internal List<GraphPlanNode> Children { get; } = [];
    internal Statement RootStatement { get; set; } = null!;
}
