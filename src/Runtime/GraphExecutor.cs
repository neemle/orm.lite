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
        var roots = await GraphStatementRunner.ReadAsync(context, plan.RootStatement,
            reader => GraphFrontier.Typed<T>(context.Identity.Intern(plan.Root.Binding, plan.Query.Map(reader))), true);
        if (roots.Count != 0) await ExpandAsync(context, plan.Children, roots.Cast<object>().ToList());
        return roots;
    }

    private static async Task ExpandAsync(GraphReadContext context, IEnumerable<GraphPlanNode> nodes,
        IReadOnlyList<object> parents)
    {
        foreach (var node in nodes)
        {
            var children = await node.Edge.LoadAsync(context, node, parents);
            if (children.Count != 0 && node.Children.Count != 0)
                await ExpandAsync(context, node.Children, children);
        }
    }
}
