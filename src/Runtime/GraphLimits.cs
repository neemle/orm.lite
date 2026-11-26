namespace Neemle.OrmLite;

public sealed record GraphLimits
{
    public GraphLimits(GraphTraversalLimits traversal, GraphWorkLimits work)
    {
        if (traversal is null || work is null)
            throw new GraphRequestException(GraphRequestError.InvalidLimits);
        Traversal = traversal with { };
        Work = work with { };
        if (MaxDepth <= 0 || MaxPaths <= 0 || MaxNodes <= 0 || MaxRoots <= 0
            || MaxRelatedRows <= 0 || MaxStatements <= 0)
            throw new GraphRequestException(GraphRequestError.InvalidLimits);
    }

    public GraphTraversalLimits Traversal { get; }
    public GraphWorkLimits Work { get; }
    public int MaxDepth => Traversal.MaxDepth;
    public int MaxPaths => Traversal.MaxPaths;
    public int MaxNodes => Traversal.MaxNodes;
    public int MaxRoots => Work.MaxRoots;
    public int MaxRelatedRows => Work.MaxRelatedRows;
    public int MaxStatements => Work.MaxStatements;
}
