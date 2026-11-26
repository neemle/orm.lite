namespace Neemle.OrmLite;

public static class GraphQueryExtensions
{
    public static async Task<List<T>> ToGraphListAsync<T>(this Query<T> query, GraphRequest<T> request,
        CancellationToken cancellationToken = default) where T : class
    {
        using var registry = GraphModel.Enter();
        var plan = GraphPlanner.Build(query, request);
        return await GraphExecutor.ExecuteAsync(plan, cancellationToken);
    }
}
