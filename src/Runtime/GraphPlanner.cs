using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class GraphPlanner
{
    internal static GraphExecutionPlan<T> Build<T>(Query<T> query, GraphRequest<T> request) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);
        return Build(query.GraphSnapshot(), request);
    }

    internal static GraphExecutionPlan<T> Build<T>(GraphQueryState<T> state, GraphRequest<T> request) where T : class
    {
        var registry = GraphModel.Snapshot();
        var root = registry.Get(typeof(T));
        if (state.Unsupported || state.Root.From?.Table != root.Table || !root.Binding.Maps(state.Map))
            throw new GraphRequestException(GraphRequestError.UnsupportedShape);
        var plan = new GraphExecutionPlan<T>(state, request.Limits, root);
        CheckRoot(plan);
        BuildPaths(plan, request.Paths, registry);
        var select = state.Root;
        if (select.Limit is null && request.Limits.MaxRoots < int.MaxValue)
            select = select with { Limit = new Limit(request.Limits.MaxRoots + 1) };
        plan.RootStatement = StatementCompiler.Compile(select, state.Database.Provider, StatementKind.Read, root.Table);
        CheckCapacity(plan.RootStatement.Parameters.Count, state.Database.Provider.MaxBindParameters);
        return plan;
    }

    internal static GraphPlanNode PolicyNode<T>(GraphExecutionPlan<T> plan, GraphEntitySnapshot source,
        IGraphNavigationBinding edge) where T : class => AddNode(plan, [], source, edge, GraphModel.Snapshot());

    private static void CheckRoot<T>(GraphExecutionPlan<T> plan) where T : class
    {
        var limit = plan.Query.Root.Limit?.Count;
        if (limit > plan.Limits.MaxRoots)
            throw new GraphLimitExceededException(GraphBudget.Roots, plan.Limits.MaxRoots);
        GraphVisibility.Require(plan.Root, plan.Root.Binding.KeyColumn, "", plan.Query.Masks);
    }

    private static void BuildPaths<T>(GraphExecutionPlan<T> plan, IReadOnlyList<GraphPath<T>> paths,
        GraphRegistrySnapshot registry) where T : class
    {
        var complete = new HashSet<GraphPlanNode>();
        foreach (var path in paths)
        {
            if (path.Edges.Count == 0) throw new GraphRequestException(GraphRequestError.InvalidPath);
            Bound(path.Edges.Count, plan.Limits.MaxDepth, GraphBudget.Depth);
            var children = plan.Children;
            var source = plan.Root;
            GraphPlanNode? last = null;
            foreach (var edge in path.Edges)
            {
                last = AddNode(plan, children, source, edge, registry);
                children = last.Children;
                source = last.Target;
            }
            complete.Add(last!);
            Bound(complete.Count, plan.Limits.MaxPaths, GraphBudget.Paths);
        }
    }

    private static GraphPlanNode AddNode<T>(GraphExecutionPlan<T> plan, List<GraphPlanNode> children,
        GraphEntitySnapshot source, IGraphNavigationBinding edge, GraphRegistrySnapshot registry) where T : class
    {
        if (edge.Source != source.Binding.EntityType || !source.Navigations.Contains(edge))
            throw new GraphRequestException(GraphRequestError.InvalidPath);
        var existing = children.Find(n => ReferenceEquals(n.Edge, edge));
        if (existing is not null) return existing;
        Bound(++plan.NodeCount, plan.Limits.MaxNodes, GraphBudget.Nodes);
        var target = registry.Get(edge.Target);
        edge.Validate(source.Binding, target.Binding);
        var node = new GraphPlanNode(edge, target, edge.Navigation);
        CheckVisibility(plan, source, node);
        var masks = edge.Target == typeof(T) ? plan.Query.Masks : new Dictionary<string, Node>();
        node.MayBePartial = masks.Count != 0;
        node.Shape = new Select(target.Projection(masks), new TableRef(target.Table),
            target.SoftColumn is null ? null : new Where([new IsNull(new ColumnRef(target.SoftColumn))]));
        node.ReservedParameters = StatementCompiler.Compile(node.Shape, plan.Query.Database.Provider,
            StatementKind.Read, target.Table).Parameters.Count;
        if (node.ReservedParameters >= plan.Query.Database.Provider.MaxBindParameters)
            throw new GraphRequestException(GraphRequestError.NoBindCapacity, node.Path);
        children.Add(node);
        return node;
    }

    private static void CheckVisibility<T>(GraphExecutionPlan<T> plan, GraphEntitySnapshot source,
        GraphPlanNode node) where T : class
    {
        if (source.Binding.EntityType == typeof(T) && node.Edge.SourceColumn is { } sourceColumn)
            GraphVisibility.Require(source, sourceColumn, node.Path, plan.Query.Masks);
        if (node.Target.Binding.EntityType != typeof(T)) return;
        GraphVisibility.Require(node.Target, node.Target.Binding.KeyColumn, node.Path, plan.Query.Masks);
        if (node.Edge.TargetColumn is { } targetColumn)
            GraphVisibility.Require(node.Target, targetColumn, node.Path, plan.Query.Masks);
    }

    private static void Bound(long value, long limit, GraphBudget budget)
    { if (value > limit) throw new GraphLimitExceededException(budget, limit); }
    private static void CheckCapacity(int count, int capacity)
    { if (count > capacity) throw new GraphRequestException(GraphRequestError.NoBindCapacity); }
}
