namespace Neemle.OrmLite;

public static class RelationPolicies
{
    private static readonly object Gate = new();
    private static readonly Dictionary<Type, PolicyModel> Models = [];

    public static void Register<T>(GraphLimits limits, params RelationPolicy<T>[] policies) where T : class
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (policies is null || policies.Any(p => p is null)) throw Invalid();
        var binding = GraphModel.Find(typeof(T));
        var modes = new Dictionary<IGraphNavigationBinding, RelationLoadMode>();
        foreach (var policy in policies)
        {
            if (!binding.Navigations.Contains(policy.Edge) || !modes.TryAdd(policy.Edge, policy.Mode)) throw Invalid();
            policy.Edge.Validate(binding, GraphModel.Find(policy.Edge.Target));
        }
        var model = new PolicyModel(limits, modes);
        lock (Gate)
        {
            if (Models.TryGetValue(typeof(T), out var old))
            {
                if (old.Equivalent(model)) return;
                throw Invalid();
            }
            Models.Add(typeof(T), model);
            PolicyReadHooks<T>.Rows = PolicyReader<T>.RowsAsync;
            PolicyReadHooks<T>.First = PolicyReader<T>.FirstAsync;
        }
    }

    internal static PolicyModel? Find(Type type)
    { lock (Gate) return Models.GetValueOrDefault(type); }

    private static GraphRequestException Invalid() => new(GraphRequestError.InvalidBinding);
}
