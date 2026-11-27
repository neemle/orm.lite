namespace Neemle.OrmLite;

internal sealed class JoinedReadPlan(Statement statement, JoinedReadLayout layout, JoinQueryPlan query)
{
    internal Statement Statement { get; } = statement with
    { Parameters = Array.AsReadOnly(statement.Parameters.ToArray()) };
    internal JoinedReadLayout Layout { get; } = layout;
    internal QueryJoinSnapshot Root => query.Root;
    internal int MaxResultRows => query.Limits.MaxResultRows;
    internal bool Empty => query.EffectiveLimit == 0;
    internal void ValidateCurrent() => JoinPlanChecks.Full(query);
}
