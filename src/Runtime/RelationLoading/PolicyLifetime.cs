namespace Neemle.OrmLite;

internal sealed class PolicyLifetime
{
    private readonly object _gate = new();
    private readonly HashSet<LoadingScope> _scopes = [];
    private bool _closed;

    internal void Add(LoadingScope scope)
    {
        lock (_gate)
        {
            if (_closed) throw new RelationScopeException(RelationScopeError.Closed);
            _scopes.Add(scope);
        }
    }

    internal void Remove(LoadingScope scope) { lock (_gate) _scopes.Remove(scope); }

    internal Task CloseAsync()
    {
        LoadingScope[] scopes;
        lock (_gate) { _closed = true; scopes = _scopes.ToArray(); }
        return Task.WhenAll(scopes.Select(scope => scope.CloseFromOwnerAsync()));
    }
}
