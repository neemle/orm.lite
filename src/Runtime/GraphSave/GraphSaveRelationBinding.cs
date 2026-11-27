namespace Neemle.OrmLite;

public abstract class GraphSaveRelationBinding<T> where T : class
{
    internal GraphSaveRelationBinding() { }
    internal abstract IGraphSaveRelation Binding { get; }
}
