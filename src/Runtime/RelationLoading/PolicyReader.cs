namespace Neemle.OrmLite;

internal static class PolicyReader<T> where T : class
{
    internal static Task<List<T>> RowsAsync(Query<T> query, RelationReadOptions options, CancellationToken ct) =>
        ReadAsync(query.GraphSnapshot(), options, ct);

    internal static async Task<T?> FirstAsync(Query<T> query, RelationReadOptions options, CancellationToken ct)
    {
        var rows = await ReadAsync(query.GraphFirstSnapshot(), options, ct);
        return rows.FirstOrDefault();
    }

    internal static Task<List<T>> SnapshotAsync(GraphQueryState<T> state, RelationReadOptions options,
        CancellationToken ct) => ReadAsync(state, options, ct);

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
        var scopeOptions = new LoadingScopeOptions(limits, 1, TimeSpan.MaxValue);
        await using var owned = state.Connection is { } connection
            ? LoadingScope.Bind(state.Database, connection, state.Transaction, scopeOptions)
            : await LoadingScope.OpenAsync(state.Database, scopeOptions, ct);
        return await InScopeAsync(state, options, owned, limits, ct);
    }

    private static Task<List<T>> InScopeAsync(GraphQueryState<T> state, RelationReadOptions options,
        LoadingScope scope, GraphLimits limits, CancellationToken ct)
    {
        scope.Binding.Match(state);
        var session = new PolicySession<T>(state, limits);
        return scope.Scheduler.RunAsync(async token =>
        {
            using var registry = GraphModel.Enter();
            using var policies = RelationPolicies.Enter();
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
}
