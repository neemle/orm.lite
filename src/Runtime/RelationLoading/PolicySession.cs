namespace Neemle.OrmLite;

internal sealed class PolicySession<T>(GraphQueryState<T> state, GraphLimits limits) : IPolicySession where T : class
{
    public GraphLimits Limits { get; } = limits;

    private readonly List<PolicyKnownEntity> _known = [];

    public IPolicyOperation Operation(GraphReadContext context) => new PolicyOperation<T>(context,
        GraphPlanner.Build(state, new GraphRequest<T>(context.Limits)));

    public void Remember(object entity, IGraphEntityBinding binding) => _known.Add(new PolicyKnownEntity(entity, binding));

    public void Seed(GraphReadContext context)
    {
        foreach (var known in _known) known.Seed(context);
    }
}
