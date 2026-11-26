namespace Neemle.OrmLite;

internal interface IPolicyOperation
{
    IGraphEntityBinding RootBinding { get; }
    Task<List<object>> RootsAsync();
    GraphPlanNode Node(IGraphEntityBinding source, IGraphNavigationBinding edge);
    GraphReadContext Context { get; }
    GraphLimits Limits { get; }
}
