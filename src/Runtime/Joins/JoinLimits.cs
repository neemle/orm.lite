namespace Neemle.OrmLite;

public sealed record JoinLimits(int MaxSources, int MaxProjectionColumns, int MaxPredicateNodes,
    int MaxParameters, int MaxResultRows)
{
    public int MaxSources { get; } = Sources(MaxSources);
    public int MaxProjectionColumns { get; } = Positive(MaxProjectionColumns);
    public int MaxPredicateNodes { get; } = Positive(MaxPredicateNodes);
    public int MaxParameters { get; } = Positive(MaxParameters);
    public int MaxResultRows { get; } = Positive(MaxResultRows);

    private static int Sources(int value) => value <= 4 ? Positive(value)
        : throw new JoinReadException(JoinError.InvalidRequest);
    private static int Positive(int value) => value > 0 ? value
        : throw new JoinReadException(JoinError.InvalidRequest);
}
