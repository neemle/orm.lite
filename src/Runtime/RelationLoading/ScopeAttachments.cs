using System.Runtime.CompilerServices;

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
        var loading = demand.State == RelationLoadState.Loading;
        demand.State = publication.Empty ? RelationLoadState.LoadedEmpty : RelationLoadState.LoadedValue;
        if (!loading) demand.Completion.TrySetResult();
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
}