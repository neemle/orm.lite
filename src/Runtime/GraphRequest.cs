namespace Neemle.OrmLite;

public sealed class GraphRequest<TRoot> where TRoot : class
{
    public GraphRequest(GraphLimits limits, params GraphPath<TRoot>[] paths)
    {
        Limits = limits ?? throw new GraphRequestException(GraphRequestError.InvalidLimits);
        if (paths is null || paths.Any(p => p is null))
            throw new GraphRequestException(GraphRequestError.InvalidPath);
        Paths = Array.AsReadOnly(paths.ToArray());
    }

    public GraphLimits Limits { get; }
    public IReadOnlyList<GraphPath<TRoot>> Paths { get; }
}
