namespace Neemle.OrmLite;

internal sealed class GraphSaveBudget
{
    private readonly GraphSaveLimits _limits;
    private long _nodes;
    private long _edges;

    internal GraphSaveBudget(GraphSaveLimits limits)
    {
        if (limits?.Traversal is not { } traversal || limits.Work is not { } work)
            throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        if (new[] { traversal.MaxDepth, traversal.MaxNodes, traversal.MaxEdges,
                work.MaxEntityWrites, work.MaxLinkAdds, work.MaxStatements }.Any(n => n <= 0))
            throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        _limits = limits;
    }

    internal void Node(int depth)
    {
        if (depth > _limits.Traversal.MaxDepth || ++_nodes > _limits.Traversal.MaxNodes)
            throw GraphSaveRefusal.At(GraphSaveError.LimitExceeded);
    }

    internal void Edge()
    {
        if (++_edges > _limits.Traversal.MaxEdges) throw GraphSaveRefusal.At(GraphSaveError.LimitExceeded);
    }

    internal void Work(int writes, int links, int anchors)
    {
        if (writes > _limits.Work.MaxEntityWrites || links > _limits.Work.MaxLinkAdds
            || (long)writes + links + anchors > _limits.Work.MaxStatements)
            throw GraphSaveRefusal.At(GraphSaveError.LimitExceeded);
    }
}
