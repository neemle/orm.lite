namespace Neemle.OrmLite;

internal static class PolicyLimits
{
    internal static GraphLimits Intersect(GraphLimits left, GraphLimits right) => new(
        new GraphTraversalLimits(Math.Min(left.MaxDepth, right.MaxDepth), Math.Min(left.MaxPaths, right.MaxPaths),
            Math.Min(left.MaxNodes, right.MaxNodes)),
        new GraphWorkLimits(Math.Min(left.MaxRoots, right.MaxRoots), Math.Min(left.MaxRelatedRows, right.MaxRelatedRows),
            Math.Min(left.MaxStatements, right.MaxStatements)));
}
