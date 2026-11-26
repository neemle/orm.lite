namespace Neemle.OrmLite;

public sealed class SaveRelation<TSource, TTarget> where TSource : class where TTarget : class
{
    internal SaveRelation() { }
    internal IGraphSaveRelation Binding { get; init; } = null!;
}
