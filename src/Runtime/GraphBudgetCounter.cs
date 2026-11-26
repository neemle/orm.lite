namespace Neemle.OrmLite;

internal sealed class GraphBudgetCounter(GraphLimits limits)
{
    private long _statements;
    private long _related;
    private long _roots;
    internal long RelatedRemaining => limits.MaxRelatedRows - _related;

    internal void Statement()
    {
        if (_statements >= limits.MaxStatements)
            throw new GraphLimitExceededException(GraphBudget.Statements, limits.MaxStatements);
        _statements++;
    }

    internal void Row(bool root)
    {
        if (root)
        {
            if (++_roots > limits.MaxRoots)
                throw new GraphLimitExceededException(GraphBudget.Roots, limits.MaxRoots);
        }
        else if (++_related > limits.MaxRelatedRows)
            throw new GraphLimitExceededException(GraphBudget.RelatedRows, limits.MaxRelatedRows);
    }
}
