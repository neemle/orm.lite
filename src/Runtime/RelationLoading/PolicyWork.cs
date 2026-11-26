namespace Neemle.OrmLite;

internal sealed class PolicyWork
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
            { Publication = Stage, SourceKeys = scope.Attachments.SourceKey };
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
}
