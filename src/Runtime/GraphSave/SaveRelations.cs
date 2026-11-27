namespace Neemle.OrmLite;

public static class SaveRelations
{
    public static SaveRelation<TSource, TTarget> Edge<TSource, TTarget>(string navigation)
        where TSource : class where TTarget : class =>
        new() { Binding = GraphSaveModel.Edge(typeof(TSource), typeof(TTarget), navigation) };
}
