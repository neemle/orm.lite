namespace Neemle.OrmLite;

internal static class OrphanApply<TChild, TKey> where TChild : class where TKey : notnull
{
    internal static async Task<OrphanResult> RunAsync(OrphanUnitAcquire owner, IReadOnlyList<TKey> original,
        OrphanReplacementRequest<TChild, TKey> request, CancellationToken ct)
    {
        OrphanResult result;
        try
        {
            var plan = OrphanPlan<TChild, TKey>.Build(owner.Operation, original, request);
            var commands = OrphanStatements.Build(owner.Operation, plan);
            result = OrphanPlannedResult.Build(owner.Operation.Options.Mode, plan);
            ct.ThrowIfCancellationRequested();
            owner.Active();
            await owner.Guard!.VerifyEffectsAsync(owner.Operation.Options.Mode, ct);
            ct.ThrowIfCancellationRequested();
            owner.Active();
            owner.Budget.ObserveGuard(owner.Guard.StatementsUsed);
            owner.Budget.Require(commands.Count);
            foreach (var command in commands) await OrphanCommandExecution.RunAsync(owner, command, ct);
            owner.Active();
            ct.ThrowIfCancellationRequested();
        }
        catch (Exception primary) { throw await OrphanFinish.FailedAsync(owner, primary, ct); }
        return await OrphanFinish.CommitAsync(owner, result);
    }
}
