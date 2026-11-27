namespace Neemle.OrmLite;

internal static class OrphanBegin
{
    internal static Task<OrphanReplacement<TChild, TKey>> Start<TChild, TKey>(OrphanOperation operation,
        CancellationToken ct) where TChild : class where TKey : notnull
    {
        if (ct.IsCancellationRequested)
            throw new OrphanCanceledException(OrphanOutcome.NotStarted, ct, new OperationCanceledException(ct));
        if (operation.Options.Mode == OrphanMode.Disabled)
            return Task.FromResult(new OrphanReplacement<TChild, TKey>(operation, null, []));
        var guard = operation.EnabledGuard();
        return EnabledAsync<TChild, TKey>(operation, guard, ct);
    }

    private static async Task<OrphanReplacement<TChild, TKey>> EnabledAsync<TChild, TKey>(OrphanOperation operation,
        IOrphanSnapshotGuard guard, CancellationToken ct) where TChild : class where TKey : notnull
    {
        var owner = new OrphanUnitAcquire(operation);
        try
        {
            await owner.StartAsync(ct);
            owner.Guard = await guard.AcquireAsync(owner.Unit!.Connection, owner.Unit.Transaction,
                operation.Request, ct) ?? throw OrphanRefusal.At(OrphanError.UnsupportedGuard);
            ct.ThrowIfCancellationRequested();
            owner.Active();
            owner.Budget.ObserveGuard(owner.Guard.StatementsUsed);
            var original = OrphanAuthority<TKey>.Copy(operation, owner.Guard);
            return new OrphanReplacement<TChild, TKey>(operation, owner, original);
        }
        catch (Exception primary) { throw await OrphanFinish.FailedAsync(owner, primary, ct); }
    }
}
