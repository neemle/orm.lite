from pathlib import Path
p=Path('src/Runtime/RelationLoading')
f={
'PolicyKnownEntity.cs':'''internal sealed class PolicyKnownEntity(object entity, IGraphEntityBinding binding)
{
    internal WeakReference<object> Entity { get; } = new(entity);
    internal IGraphEntityBinding Binding { get; } = binding;
    private readonly object _identity = binding.Key(entity);

    internal void Seed(GraphReadContext context)
    {
        if (Entity.TryGetTarget(out var entity) && Binding.Comparer.Equals(_identity, Binding.Key(entity)))
            context.Identity.Intern(Binding, entity);
    }
}''',
'PolicySession.cs':'''internal sealed class PolicySession<T>(GraphQueryState<T> state) : IPolicySession where T : class
{
    private readonly List<PolicyKnownEntity> _known = [];

    public IPolicyOperation Operation(GraphReadContext context) => new PolicyOperation<T>(context,
        GraphPlanner.Build(state, new GraphRequest<T>(context.Limits)));

    public void Remember(object entity, IGraphEntityBinding binding) => _known.Add(new PolicyKnownEntity(entity, binding));

    public void Seed(GraphReadContext context)
    {
        foreach (var known in _known) known.Seed(context);
    }
}''',
'PolicyOperation.cs':'''internal sealed class PolicyOperation<T>(GraphReadContext context, GraphExecutionPlan<T> plan)
    : IPolicyOperation where T : class
{
    public GraphReadContext Context { get; } = context;
    public GraphLimits Limits => plan.Limits;
    public IGraphEntityBinding RootBinding => plan.Root.Binding;

    public GraphPlanNode Node(IGraphEntityBinding source, IGraphNavigationBinding edge) =>
        GraphPlanner.PolicyNode(plan, new GraphEntitySnapshot(source), edge);

    public async Task<List<object>> RootsAsync() =>
        (await GraphExecutor.ReadRootsAsync(plan, Context)).Cast<object>().ToList();
}''',
'PolicyPreflight.cs':'''internal static class PolicyPreflight
{
    internal static void Validate(IPolicyOperation operation, IGraphEntityBinding root)
    {
        var seen = new HashSet<Type>();
        var queue = new Queue<IGraphEntityBinding>();
        queue.Enqueue(root);
        while (queue.TryDequeue(out var source))
        {
            if (!seen.Add(source.EntityType)) continue;
            var policy = RelationPolicies.Find(source.EntityType);
            if (policy is null) continue;
            foreach (var edge in policy.Eager)
            {
                operation.Node(source, edge);
                queue.Enqueue(GraphModel.Find(edge.Target));
            }
        }
    }
}''',
'PolicyFrontier.cs':'''internal sealed record PolicyFrontier(IGraphEntityBinding Binding, IReadOnlyList<object> Parents,
    int Depth, string Path);''',
'PolicyTraversal.cs':'''internal sealed class PolicyTraversal(PolicyWork work)
{
    private readonly Dictionary<object, HashSet<IGraphNavigationBinding>> _seen =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<string> _paths = [];
    private readonly Queue<PolicyFrontier> _frontiers = new();

    internal async Task EagerAsync(IGraphEntityBinding binding, IReadOnlyList<object> parents)
    {
        _frontiers.Enqueue(new PolicyFrontier(binding, parents, 0, ""));
        while (_frontiers.TryDequeue(out var frontier))
        {
            var model = RelationPolicies.Find(frontier.Binding.EntityType);
            if (model is null) continue;
            foreach (var edge in model.Eager) await EdgeAsync(frontier, edge);
        }
    }

    internal async Task EdgeAsync(PolicyFrontier frontier, IGraphNavigationBinding edge)
    {
        var parents = frontier.Parents.Where(parent => Visit(parent, edge)).ToList();
        if (parents.Count == 0) return;
        var path = frontier.Path + "/" + edge.Navigation;
        if (frontier.Depth >= work.Operation.Limits.MaxDepth)
            throw new GraphLimitExceededException(GraphBudget.Depth, work.Operation.Limits.MaxDepth);
        if (_paths.Add(path) && _paths.Count > work.Operation.Limits.MaxPaths)
            throw new GraphLimitExceededException(GraphBudget.Paths, work.Operation.Limits.MaxPaths);
        var node = work.Operation.Node(frontier.Binding, edge);
        var rows = await edge.LoadAsync(work.Context, node, parents);
        work.Track(rows, node.Target.Binding);
        if (rows.Count != 0) _frontiers.Enqueue(new PolicyFrontier(node.Target.Binding, rows, frontier.Depth + 1, path));
    }

    private bool Visit(object parent, IGraphNavigationBinding edge)
    {
        if (!_seen.TryGetValue(parent, out var edges)) _seen.Add(parent, edges = []);
        return edges.Add(edge);
    }

    internal async Task DemandAsync(PolicyDemandWork demand)
    {
        await EdgeAsync(new PolicyFrontier(demand.Attachment.Binding, [demand.Entity], 0, ""), demand.Edge);
        while (_frontiers.TryDequeue(out var frontier))
        {
            var model = RelationPolicies.Find(frontier.Binding.EntityType);
            if (model is null) continue;
            foreach (var edge in model.Eager) await EdgeAsync(frontier, edge);
        }
    }
}''',
'PolicyWork.cs':'''internal sealed class PolicyWork
{
    private readonly LoadingScope _scope;
    private readonly IPolicySession _session;
    private readonly Dictionary<object, IGraphEntityBinding> _entities = new(ReferenceEqualityComparer.Instance);
    private readonly List<PolicyPublication> _publications = [];
    internal GraphReadContext Context { get; }
    internal IPolicyOperation Operation { get; }

    internal PolicyWork(LoadingScope scope, IPolicySession session, GraphLimits limits, CancellationToken ct)
    {
        _scope = scope;
        _session = session;
        var binding = scope.Binding;
        Context = new GraphReadContext(binding.Context, binding.Connection, binding.Transaction, limits, ct)
            { Publication = Stage };
        session.Seed(Context);
        Operation = session.Operation(Context);
    }

    private void Stage(object parent, IGraphNavigationBinding edge, object? value, Action publish) =>
        _publications.Add(new PolicyPublication(parent, edge, value, publish));

    internal void Track(IEnumerable<object> rows, IGraphEntityBinding binding)
    { foreach (var row in rows) _entities.TryAdd(row, binding); }

    internal void Publish(PolicyDemandWork? demand = null)
    {
        lock (_scope.Gate)
        {
            _scope.Scheduler.Validate();
            Context.CancellationToken.ThrowIfCancellationRequested();
            if (demand is not null) _scope.Attachments.Validate(demand.Entity, demand.Generation);
            foreach (var pair in _entities) _scope.Attachments.Attach(pair.Key, pair.Value, _session);
            foreach (var publication in Enumerable.Reverse(_publications)) publication.Apply();
            foreach (var publication in _publications) _scope.Attachments.Published(publication);
        }
    }
}''',
'PolicyDemandRunner.cs':'''internal static class PolicyDemandRunner
{
    internal static async Task<bool> RunAsync(LoadingScope scope, PolicyDemandWork demand, CancellationToken ct)
    {
        using var registry = GraphModel.Enter();
        var limits = scope.Binding.Options.Limits;
        var model = RelationPolicies.Find(demand.Attachment.Binding.EntityType);
        if (model is not null) limits = PolicyLimits.Intersect(limits, model.Limits);
        var work = new PolicyWork(scope, demand.Attachment.Session, limits, ct);
        PolicyPreflight.Validate(demand.Attachment.Session.Operation(work.Context), GraphModel.Find(demand.Edge.Target));
        work.Track([demand.Entity], demand.Attachment.Binding);
        await new PolicyTraversal(work).DemandAsync(demand);
        work.Publish(demand);
        return true;
    }
}''',
'PolicyReader.cs':'''internal static class PolicyReader<T> where T : class
{
    internal static Task<List<T>> RowsAsync(Query<T> query, RelationReadOptions options, CancellationToken ct) =>
        ReadAsync(query.GraphSnapshot(), options, ct);

    internal static async Task<T?> FirstAsync(Query<T> query, RelationReadOptions options, CancellationToken ct)
    {
        var rows = await ReadAsync(query.GraphFirstSnapshot(), options, ct);
        return rows.FirstOrDefault();
    }

    private static async Task<List<T>> ReadAsync(GraphQueryState<T> state, RelationReadOptions options,
        CancellationToken ct)
    {
        if (options is null || !Enum.IsDefined(options.Eager))
            throw new RelationScopeException(RelationScopeError.InvalidOptions);
        options = options with { };
        var model = RelationPolicies.Find(typeof(T));
        if (model is null && options.Scope is null) return await PolicyPlainReader.ReadAsync(state, ct);
        var limits = options.Scope?.Binding.Options.Limits ?? model!.Limits;
        if (model is not null) limits = PolicyLimits.Intersect(limits, model.Limits);
        if (options.Scope is { } scope) return await InScopeAsync(state, options, scope, limits, ct);
        await using var owned = await LoadingScope.OpenAsync(state.Database,
            new LoadingScopeOptions(limits, 1, TimeSpan.MaxValue), ct);
        return await InScopeAsync(state, options, owned, limits, ct);
    }

    private static Task<List<T>> InScopeAsync(GraphQueryState<T> state, RelationReadOptions options,
        LoadingScope scope, GraphLimits limits, CancellationToken ct)
    {
        scope.Binding.Match(state);
        var session = new PolicySession<T>(state);
        return scope.Scheduler.RunAsync(async token =>
        {
            using var registry = GraphModel.Enter();
            var work = new PolicyWork(scope, session, limits, token);
            if (options.Eager == EagerBehavior.UseModel)
                PolicyPreflight.Validate(session.Operation(work.Context), work.Operation.RootBinding);
            var roots = await work.Operation.RootsAsync();
            work.Track(roots, work.Operation.RootBinding);
            if (options.Eager == EagerBehavior.UseModel)
                await new PolicyTraversal(work).EagerAsync(work.Operation.RootBinding, roots);
            work.Publish();
            return roots.Cast<T>().ToList();
        }, ct);
    }
}''',
'PolicyPlainReader.cs':'''namespace Neemle.OrmLite;

internal static class PolicyPlainReader
{
    internal static async Task<List<T>> ReadAsync<T>(GraphQueryState<T> state, CancellationToken ct) where T : class
    {
        if (state.Unsupported || !GraphModel.Find(typeof(T)).Maps(state.Map))
            throw new GraphRequestException(GraphRequestError.UnsupportedShape);
        if (state.Connection is { } connection) return await ReadBoundAsync(state, connection, ct);
        await using var owned = await state.Database.DataSource.OpenConnectionAsync(ct);
        return await ReadBoundAsync(state, owned, ct);
    }

    private static async Task<List<T>> ReadBoundAsync<T>(GraphQueryState<T> state,
        System.Data.Common.DbConnection connection, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var statement = StatementCompiler.Compile(state.Root, state.Database.Provider, StatementKind.Read);
        await using var command = StatementCommand.Create(state.Database.DataSource, connection,
            state.Transaction, statement);
        foreach (var parameter in statement.Parameters)
            command.AddParam(parameter.Name, parameter.Value ?? DBNull.Value);
        await using var reader = await command.RunReaderAsync(StatementKind.Read, ct);
        var rows = new List<T>();
        while (await reader.ReadAsync(ct)) rows.Add(state.Map(reader));
        return rows;
    }
}'''
}
for n,s in f.items():(p/n).write_text(s if s.startswith('namespace ') else 'namespace Neemle.OrmLite;\n\n'+s+'\n')
p=Path('src/Runtime/GraphReadContext.cs');s=p.read_text().replace('    internal GraphBudgetCounter Budget','    internal GraphLimits Limits { get; } = limits;\n    internal GraphBudgetCounter Budget');p.write_text(s)
p=Path('src/Runtime/RelationLoading/IPolicyOperation.cs');s=p.read_text().replace('    GraphPlanNode Node','    IGraphEntityBinding RootBinding { get; }\n    Task<List<object>> RootsAsync();\n    GraphPlanNode Node');p.write_text(s)
p=Path('src/Runtime/RelationLoading/ScopeAttachments.cs');s=p.read_text().replace('''        demand.State = publication.Empty''','''        var loading = demand.State == RelationLoadState.Loading;
        demand.State = publication.Empty''').replace('''        demand.Completion.TrySetResult();
    }

    internal Task Demand''','''        if (!loading) demand.Completion.TrySetResult();
    }

    internal Task Demand''');p.write_text(s)
