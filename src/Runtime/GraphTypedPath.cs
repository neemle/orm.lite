namespace Neemle.OrmLite;

public sealed class GraphPath<TRoot, TCurrent> : GraphPath<TRoot>
    where TRoot : class where TCurrent : class
{
    internal GraphPath() { }
    internal GraphPath(IReadOnlyList<IGraphNavigationBinding> edges) { Edges = edges; }
    internal override IReadOnlyList<IGraphNavigationBinding> Edges { get; } = [];

    public GraphPath<TRoot, TNext> Then<TNext>(GraphPath<TCurrent, TNext> edge) where TNext : class
    {
        if (edge is null || edge.Edges.Count != 1 || Edges.Count == 0)
            throw new GraphRequestException(GraphRequestError.InvalidPath);
        return new GraphPath<TRoot, TNext>(Array.AsReadOnly(Edges.Concat(edge.Edges).ToArray()));
    }
}
