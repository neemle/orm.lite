namespace Neemle.OrmLite;

internal sealed class PolicyOperation<T>(GraphReadContext context, GraphExecutionPlan<T> plan)
    : IPolicyOperation where T : class
{
    public GraphReadContext Context { get; } = context;
    public GraphLimits Limits => plan.Limits;
    public IGraphEntityBinding RootBinding => plan.Root.Binding;

    public GraphPlanNode Node(IGraphEntityBinding source, IGraphNavigationBinding edge) =>
        GraphPlanner.PolicyNode(plan, new GraphEntitySnapshot(source), edge);

    public async Task<List<object>> RootsAsync() =>
        (await GraphExecutor.ReadRootsAsync(plan, Context,
            OneToOneReadMap.Wrap(plan.Query.Map, plan.Query.Masks.Count != 0))).Cast<object>().ToList();
}
