namespace Neemle.OrmLite;

public sealed class RelationPolicy<T> where T : class
{
    private RelationPolicy(IGraphNavigationBinding edge, RelationLoadMode mode) { Edge = edge; Mode = mode; }
    internal IGraphNavigationBinding Edge { get; }
    internal RelationLoadMode Mode { get; }

    public static RelationPolicy<T> For<TTarget>(GraphPath<T, TTarget> navigation, RelationLoadMode mode)
        where TTarget : class
    {
        if (navigation is null || navigation.Edges.Count != 1 || !Enum.IsDefined(mode))
            throw new GraphRequestException(GraphRequestError.InvalidBinding);
        return new RelationPolicy<T>(navigation.Edges[0], mode);
    }
}
