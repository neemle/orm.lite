namespace Neemle.OrmLite;

internal static class GraphExecutor
{
    internal static async Task<List<T>> ExecuteAsync<T>(GraphExecutionPlan<T> plan,
        CancellationToken cancellationToken) where T : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = plan.Query;
        if (query.Connection is { } callerConnection)
            return await ReadAsync(plan, new GraphReadContext(query.Database, callerConnection,
                query.Transaction, plan.Limits, cancellationToken));
        await using var connection = await query.Database.DataSource.OpenConnectionAsync(cancellationToken);
        return await ReadAsync(plan, new GraphReadContext(query.Database, connection, null,
            plan.Limits, cancellationToken));
    }

    private static async Task<List<T>> ReadAsync<T>(GraphExecutionPlan<T> plan, GraphReadContext context)
        where T : class
    {
        var roots = await ReadRootsAsync(plan, context);
        if (roots.Count != 0) await ExpandAsync(context, plan.Children, roots.Cast<object>().ToList());
        return roots;
    }

    internal static Task<List<T>> ReadRootsAsync<T>(GraphExecutionPlan<T> plan, GraphReadContext context)
        where T : class => ReadRootsAsync(plan, context, plan.Query.Map);

    internal static Task<List<T>> ReadRootsAsync<T>(GraphExecutionPlan<T> plan, GraphReadContext context,
        Func<System.Data.Common.DbDataReader, T> map) where T : class =>
        GraphStatementRunner.ReadAsync(context, plan.RootStatement,
            reader => MapRoot(plan, context, map, reader), true);

    private static T MapRoot<T>(GraphExecutionPlan<T> plan, GraphReadContext context,
        Func<System.Data.Common.DbDataReader, T> map, System.Data.Common.DbDataReader reader) where T : class
    {
        var entity = map(reader);
        var original = plan.Query.Map;
        var partial = plan.Query.Masks.Count != 0;
        GraphSaveOrigins.Observe(entity, original, partial);
        var canonical = context.Identity.Intern(plan.Root.Binding, entity);
        GraphSaveOrigins.Observe(canonical, original, partial);
        return GraphFrontier.Typed<T>(canonical);
    }

    private static async Task ExpandAsync(GraphReadContext context, IEnumerable<GraphPlanNode> nodes,
        IReadOnlyList<object> parents)
    {
        var frontiers = new Queue<(IEnumerable<GraphPlanNode> Nodes, IReadOnlyList<object> Parents)>();
        frontiers.Enqueue((nodes, parents));
        while (frontiers.TryDequeue(out var frontier))
        {
            foreach (var node in frontier.Nodes)
            {
                var children = await node.Edge.LoadAsync(context, node, frontier.Parents);
                if (children.Count != 0 && node.Children.Count != 0)
                    frontiers.Enqueue((node.Children, children));
            }
        }
    }
}
