namespace Neemle.OrmLite;

public abstract class GraphPath<TRoot> where TRoot : class
{
    internal GraphPath() { }
    internal abstract IReadOnlyList<IGraphNavigationBinding> Edges { get; }
}
