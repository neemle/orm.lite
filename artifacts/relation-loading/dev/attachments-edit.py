from pathlib import Path
p=Path('src/Runtime/RelationLoading')
f={
'IPolicySession.cs':'''internal interface IPolicySession
{
    IPolicyOperation Operation(GraphReadContext context);
    void Seed(GraphReadContext context);
    void Remember(object entity, IGraphEntityBinding binding);
}''',
'IPolicyOperation.cs':'''internal interface IPolicyOperation
{
    GraphPlanNode Node(IGraphEntityBinding source, IGraphNavigationBinding edge);
    GraphReadContext Context { get; }
    GraphLimits Limits { get; }
}''',
'PolicyPublication.cs':'''internal sealed record PolicyPublication(object Parent, IGraphNavigationBinding Edge, object? Value, Action Apply)
{
    internal bool Empty => Value is null || Value is System.Collections.ICollection { Count: 0 };
}''',
'PolicyDemand.cs':'''internal sealed class PolicyDemand
{
    internal RelationLoadState State { get; set; } = RelationLoadState.Unloaded;
    internal TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}''',
'PolicyAttachment.cs':'''internal sealed class PolicyAttachment(object entity, IGraphEntityBinding binding, IPolicySession session)
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
}''',
'ScopeAttachments.cs':'''using System.Runtime.CompilerServices;

namespace Neemle.OrmLite;

internal sealed class ScopeAttachments(LoadingScope scope)
{
    private readonly ConditionalWeakTable<object, PolicyAttachment> _entities = new();

    internal void Attach(object entity, IGraphEntityBinding binding, IPolicySession session)
    {
        if (_entities.TryGetValue(entity, out var prior) && prior.Attached) return;
        _entities.AddOrUpdate(entity, new PolicyAttachment(entity, binding, session));
        session.Remember(entity, binding);
    }

    private PolicyAttachment Get(object entity) => _entities.TryGetValue(entity, out var item) ? item
        : throw new RelationScopeException(RelationScopeError.UnboundEntity);

    internal RelationLoadSnapshot State<T, TTarget>(T entity, GraphPath<T, TTarget> navigation)
        where T : class where TTarget : class
    {
        var edge = Edge(navigation);
        lock (scope.Gate)
        {
            var item = Get(entity);
            var state = item.Demands.TryGetValue(edge, out var demand) ? demand.State : RelationLoadState.Unloaded;
            return new RelationLoadSnapshot(state, item.Attached && scope.Scheduler.IsAttached);
        }
    }

    internal void Detach(object entity)
    {
        PolicyAttachment item;
        lock (scope.Gate) { item = Get(entity); item.Attached = false; item.Generation++; }
        item.Detached.Cancel();
    }

    internal void Invalidate()
    {
        lock (scope.Gate)
            foreach (var pair in _entities) { pair.Value.Attached = false; pair.Value.Generation++; }
    }

    internal void Published(PolicyPublication publication)
    {
        var item = Get(publication.Parent);
        if (!item.Demands.TryGetValue(publication.Edge, out var demand))
            item.Demands.Add(publication.Edge, demand = new PolicyDemand());
        demand.State = publication.Empty ? RelationLoadState.LoadedEmpty : RelationLoadState.LoadedValue;
        demand.Completion.TrySetResult();
    }

    internal Task Demand<T, TTarget>(T entity, GraphPath<T, TTarget> navigation, bool retry, CancellationToken ct)
        where T : class where TTarget : class
    {
        var edge = Edge(navigation);
        lock (scope.Gate)
        {
            scope.Scheduler.Validate();
            var item = Get(entity);
            item.Validate(entity);
            if (item.Demands.TryGetValue(edge, out var existing) && !retry)
                return existing.Completion.Task.WaitAsync(ct);
            if (retry && (existing is null || existing.State is not
                (RelationLoadState.Faulted or RelationLoadState.Cancelled)))
                throw new RelationScopeException(RelationScopeError.InvalidOptions, edge.Navigation);
            var demand = new PolicyDemand { State = RelationLoadState.Loading };
            item.Demands[edge] = demand;
            _ = RunDemandAsync(entity, edge, item, demand, ct);
            return demand.Completion.Task;
        }
    }

    private async Task RunDemandAsync(object entity, IGraphNavigationBinding edge, PolicyAttachment item,
        PolicyDemand demand, CancellationToken ct)
    {
        var generation = item.Generation;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, item.Detached.Token);
        try
        {
            await scope.Scheduler.RunAsync(token => PolicyDemandRunner.RunAsync(scope,
                new PolicyDemandWork(entity, edge, item, generation), token), linked.Token);
            demand.Completion.TrySetResult();
        }
        catch (OperationCanceledException error)
        {
            lock (scope.Gate) demand.State = RelationLoadState.Cancelled;
            demand.Completion.TrySetCanceled(error.CancellationToken);
        }
        catch (Exception error)
        {
            lock (scope.Gate) demand.State = RelationLoadState.Faulted;
            demand.Completion.TrySetException(error);
        }
    }

    internal void Validate(object entity, long generation)
    {
        var item = Get(entity);
        item.Validate(entity);
        if (item.Generation != generation) throw new RelationScopeException(RelationScopeError.Detached);
    }

    private static IGraphNavigationBinding Edge<T, TTarget>(GraphPath<T, TTarget> navigation)
        where T : class where TTarget : class
    {
        if (navigation is null || navigation.Edges.Count != 1)
            throw new RelationScopeException(RelationScopeError.UnsupportedNavigation);
        return navigation.Edges[0];
    }
}''',
'PolicyDemandWork.cs':'''internal sealed record PolicyDemandWork(object Entity, IGraphNavigationBinding Edge,
    PolicyAttachment Attachment, long Generation);'''
}
for n,s in f.items():(p/n).write_text(s if s.startswith('using ') else 'namespace Neemle.OrmLite;\n\n'+s+'\n')
