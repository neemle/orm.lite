namespace Neemle.OrmLite;

public static class GraphPaths
{
    public static GraphPath<TSource, TTarget> Edge<TSource, TTarget>(string navigation)
        where TSource : class where TTarget : class
    {
        var binding = GraphModel.Find(typeof(TSource));
        var edge = binding.Navigations.SingleOrDefault(n => n.Navigation == navigation && n.Target == typeof(TTarget));
        if (edge is null) throw new GraphRequestException(GraphRequestError.InvalidPath);
        return new GraphPath<TSource, TTarget>(Array.AsReadOnly(new[] { edge }));
    }
}
