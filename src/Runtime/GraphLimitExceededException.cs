namespace Neemle.OrmLite;

public sealed class GraphLimitExceededException : InvalidOperationException
{
    internal GraphLimitExceededException(GraphBudget budget, long limit)
        : base($"Graph budget exceeded: {budget}.")
    { Budget = budget; Limit = limit; }

    public GraphBudget Budget { get; }
    public long Limit { get; }
}
