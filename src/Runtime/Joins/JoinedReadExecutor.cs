namespace Neemle.OrmLite;

internal static class JoinedReadExecutor
{
    internal static Task<List<JoinedRow>> ReadAsync(JoinedReadPlan plan, CancellationToken ct) =>
        new JoinReadExecution(plan, ct).RunAsync();
}
