from pathlib import Path
p=Path('src/Runtime/RelationLoading')
files={
'.editorconfig':'[*.cs]\ndotnet_style_namespace_match_folder = false\n',
'RelationLoadMode.cs':'public enum RelationLoadMode { Explicit, Eager, Lazy }',
'EagerBehavior.cs':'public enum EagerBehavior { UseModel, Suppress }',
'RelationLoadState.cs':'public enum RelationLoadState { Unloaded, Loading, LoadedEmpty, LoadedValue, Faulted, Cancelled }',
'RelationScopeError.cs':'''public enum RelationScopeError
{
    InvalidOptions, UnsupportedNavigation, UnboundEntity, DifferentScope, EntityChanged,
    InvalidResource, Expired, Detached, Closed, QueueFull
}''',
'RelationLoadAttribute.cs':'''[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class RelationLoadAttribute(RelationLoadMode mode) : Attribute
{
    public RelationLoadMode Mode { get; } = mode;
}''',
'RelationLoadLimitsAttribute.cs':'''[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RelationLoadLimitsAttribute : Attribute
{
    public int MaxDepth { get; set; }
    public int MaxPaths { get; set; }
    public int MaxNodes { get; set; }
    public int MaxRoots { get; set; }
    public int MaxRelatedRows { get; set; }
    public int MaxStatements { get; set; }
}''',
'RelationReadOptions.cs':'''public sealed record RelationReadOptions(EagerBehavior Eager = EagerBehavior.UseModel, LoadingScope? Scope = null);''',
'LoadingScopeOptions.cs':'''public sealed record LoadingScopeOptions(GraphLimits Limits, int MaxPendingDemands, TimeSpan Lifetime);''',
'RelationLoadSnapshot.cs':'''public sealed record RelationLoadSnapshot(RelationLoadState State, bool IsAttached);''',
'RelationScopeException.cs':'''public sealed class RelationScopeException : InvalidOperationException
{
    public RelationScopeException(RelationScopeError error, string? navigation = null)
        : base("Relation loading refused: " + error + (navigation is null ? "." : " (" + navigation + ")."))
    { Error = error; Navigation = navigation; }

    public RelationScopeError Error { get; }
    public string? Navigation { get; }
}''',
'PolicyReadHooks.cs':'''internal static class PolicyReadHooks<T>
{
    internal static Func<Query<T>, RelationReadOptions, CancellationToken, Task<List<T>>>? Rows;
    internal static Func<Query<T>, RelationReadOptions, CancellationToken, Task<T?>>? First;
}''',
'RelationQueryExtensions.cs':'''public static class RelationQueryExtensions
{
    public static Task<List<T>> ToListAsync<T>(this Query<T> query, RelationReadOptions options,
        CancellationToken ct = default) where T : class => PolicyReader<T>.RowsAsync(query, options, ct);

    public static Task<T?> FirstOrDefaultAsync<T>(this Query<T> query, RelationReadOptions options,
        CancellationToken ct = default) where T : class => PolicyReader<T>.FirstAsync(query, options, ct);
}''',
'RelationPolicy.cs':'''public sealed class RelationPolicy<T> where T : class
{
    private RelationPolicy(IGraphNavigationBinding edge, RelationLoadMode mode) { Edge = edge; Mode = mode; }
    internal IGraphNavigationBinding Edge { get; }
    internal RelationLoadMode Mode { get; }

    public static RelationPolicy<T> For<TTarget>(GraphPath<T, TTarget> navigation, RelationLoadMode mode)
        where TTarget : class
    {
        if (navigation is null || navigation.Edges.Count != 1 || !Enum.IsDefined(mode))
            throw new GraphRequestException(GraphRequestError.InvalidBinding);
        return new RelationPolicy<T>(navigation.Edges[0], mode);
    }
}''',
'PolicyModel.cs':'''internal sealed class PolicyModel(GraphLimits limits, IReadOnlyDictionary<IGraphNavigationBinding, RelationLoadMode> modes)
{
    internal GraphLimits Limits { get; } = limits;
    internal IReadOnlyDictionary<IGraphNavigationBinding, RelationLoadMode> Modes { get; } = modes;
    internal IEnumerable<IGraphNavigationBinding> Eager => Modes.Where(p => p.Value == RelationLoadMode.Eager)
        .Select(p => p.Key);

    internal bool Equivalent(PolicyModel other) => Limits == other.Limits && Modes.Count == other.Modes.Count
        && Modes.All(p => other.Modes.TryGetValue(p.Key, out var mode) && p.Value == mode);
}''',
'RelationPolicies.cs':'''public static class RelationPolicies
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
}''',
'PolicyLifetime.cs':'''internal sealed class PolicyLifetime
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
}''',
'PolicyLimits.cs':'''internal static class PolicyLimits
{
    internal static GraphLimits Intersect(GraphLimits left, GraphLimits right) => new(
        new GraphTraversalLimits(Math.Min(left.MaxDepth, right.MaxDepth), Math.Min(left.MaxPaths, right.MaxPaths),
            Math.Min(left.MaxNodes, right.MaxNodes)),
        new GraphWorkLimits(Math.Min(left.MaxRoots, right.MaxRoots), Math.Min(left.MaxRelatedRows, right.MaxRelatedRows),
            Math.Min(left.MaxStatements, right.MaxStatements)));
}'''
}
for name,body in files.items():
 (p/name).write_text(body if name=='.editorconfig' else 'namespace Neemle.OrmLite;\n\n'+body+'\n')
# Exact declared hooks: existing unconstrained generic Query surface is retained.
p=Path('src/Runtime/Query.cs');s=p.read_text().replace('''        public async Task<List<T>> ToListAsync() =>
            await _exec.RowsAsync(CompileSelect(), _map);''','''        public async Task<List<T>> ToListAsync() =>
            PolicyReadHooks<T>.Rows is { } policy
                ? await policy(this, new RelationReadOptions(), default)
                : await _exec.RowsAsync(CompileSelect(), _map);''')
s=s.replace('''        public async Task<T?> FirstOrDefaultAsync()
        {
            var rows''','''        public async Task<T?> FirstOrDefaultAsync()
        {
            if (PolicyReadHooks<T>.First is { } policy)
                return await policy(this, new RelationReadOptions(), default);
            var rows''')
pos=s.index('        internal Select BuildSelect()')
s=s[:pos]+'''        internal GraphQueryState<T> GraphFirstSnapshot() => new(_db,
            _select.BuildFirst(SelectedColumns(), WhereNode(), lockRows: true), _map)
        {
            Connection = _exec.GraphConnection,
            Transaction = _exec.GraphTransaction,
            Masks = new Dictionary<string, Node>(_state.Redactions),
            Unsupported = _projection.Count != 0,
        };

'''+s[pos:];p.write_text(s)
p=Path('src/Runtime/OrmContext.cs');s=p.read_text().replace('private readonly IReadOnlyList<IAsyncDisposable> _owned;','private readonly IReadOnlyList<IAsyncDisposable> _owned;\n\n        internal PolicyLifetime PolicyLifetime { get; } = new();').replace('''            foreach (var owned in _owned)''','''            await PolicyLifetime.CloseAsync();
            foreach (var owned in _owned)''');p.write_text(s)
p=Path('src/Runtime/UnitOfWork.cs');s=p.read_text().replace('    private bool _committed;','    internal PolicyLifetime PolicyLifetime { get; } = new();\n\n    private bool _committed;').replace('''        await _transaction.CommitAsync();''','''        await PolicyLifetime.CloseAsync();
        await _transaction.CommitAsync();''').replace('''        _disposed = true;
        try''','''        _disposed = true;
        await PolicyLifetime.CloseAsync();
        try''');p.write_text(s)
