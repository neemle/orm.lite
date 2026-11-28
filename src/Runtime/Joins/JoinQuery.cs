namespace Neemle.OrmLite;

public sealed class JoinQuery
{
    private readonly JoinQueryPlan _plan;
    internal JoinQuery(JoinQueryPlan plan) => _plan = plan;

    public JoinQuery InnerJoin<T>(JoinSource<T> right, JoinPredicate on) where T : class =>
        new(_plan.InnerJoin(right, on));
    public JoinQuery LeftJoin<T>(JoinSource<T> right, JoinPredicate on) where T : class =>
        new(_plan.LeftJoin(right, on));
    public JoinQuery Where(JoinPredicate predicate) => new(_plan.Where(predicate));
    public JoinQuery OrderBy<TValue>(JoinColumn<TValue> column, bool descending = false) =>
        new(_plan.OrderBy(column, descending));
    public JoinQuery ThenBy<TValue>(JoinColumn<TValue> column, bool descending = false) =>
        new(_plan.ThenBy(column, descending));
    public JoinQuery Skip(int count) => new(_plan.Skip(count));
    public JoinQuery Take(int count) => new(_plan.Take(count));

    public Task<List<JoinedRow>> ToListAsync(JoinSelection selection, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            throw new JoinReadCanceledException(ct, new OperationCanceledException(ct));
        var plan = JoinedReadCompiler.Build(_plan, selection);
        return JoinedReadExecutor.ReadAsync(plan, ct);
    }
}
