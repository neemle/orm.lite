namespace Neemle.OrmLite;

internal sealed class PolicyAttachment(object entity, IGraphEntityBinding binding, IPolicySession session)
{
    internal IGraphEntityBinding Binding { get; } = binding;
    internal IPolicySession Session { get; } = session;
    private readonly object _identity = binding.Key(entity);
    private readonly Dictionary<IGraphNavigationBinding, object?> _keys = binding.Navigations
        .ToDictionary(edge => edge, edge => edge.CaptureSourceKey(entity));
    internal Dictionary<IGraphNavigationBinding, PolicyDemand> Demands { get; } = [];
    internal CancellationTokenSource Detached { get; } = new();
    internal bool Attached { get; set; } = true;
    internal long Generation { get; set; }

    internal void Validate(object entity)
    {
        if (!Attached) throw new RelationScopeException(RelationScopeError.Detached);
        if (!Binding.Comparer.Equals(_identity, Binding.Key(entity))
            || _keys.Any(pair => !Equals(pair.Value, pair.Key.CaptureSourceKey(entity))))
            throw new RelationScopeException(RelationScopeError.EntityChanged);
    }
}
