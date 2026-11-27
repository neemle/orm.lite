namespace Neemle.OrmLite;

internal static class OrphanFinish
{
    internal static async Task<Exception> FailedAsync(OrphanUnitAcquire owner, Exception primary,
        CancellationToken token)
    {
        if (primary is StreamOpenFailure open)
        {
            primary = open.Primary;
            owner.Cleanup.Add(open.Cleanup);
        }
        var failure = OrphanFailure.Observe(primary, token);
        var outcome = await owner.RollbackAsync();
        await owner.CleanAsync();
        var category = outcome == OrphanOutcome.Unknown ? OrphanError.RollbackUnconfirmed : (OrphanError?)null;
        return OrphanFailure.Retain(failure.At(outcome, category), owner.Cleanup.Errors);
    }

    internal static async Task<OrphanResult> CommitAsync(OrphanUnitAcquire owner, OrphanResult result)
    {
        try { await owner.Unit!.CommitAsync(); }
        catch (Exception primary) { throw await CommitFailureAsync(owner, result, primary); }
        await owner.CleanAsync();
        if (owner.Cleanup.Errors.Count != 0)
            throw OrphanFailure.Retain(new OrphanException(OrphanError.PostCommitCleanup,
                OrphanOutcome.Committed, owner.Cleanup.Errors[0], result), owner.Cleanup.Errors);
        return result;
    }

    private static async Task<Exception> CommitFailureAsync(OrphanUnitAcquire owner, OrphanResult result,
        Exception primary)
    {
        var committed = owner.Unit!.Committed;
        if (!committed) await owner.RollbackAsync(true);
        await owner.CleanAsync();
        var failure = new OrphanException(committed ? OrphanError.PostCommitCleanup : OrphanError.CommitUnknown,
            committed ? OrphanOutcome.Committed : OrphanOutcome.Unknown, primary, committed ? result : null);
        return OrphanFailure.Retain(failure, owner.Cleanup.Errors);
    }

    internal static async Task AbandonAsync(OrphanUnitAcquire owner)
    {
        var outcome = await owner.RollbackAsync();
        await owner.CleanAsync();
        if (owner.Cleanup.Errors.Count == 0) return;
        var failure = new OrphanException(outcome == OrphanOutcome.Unknown ? OrphanError.RollbackUnconfirmed
            : OrphanError.ProviderFailure, outcome, owner.Cleanup.Errors[0]);
        throw OrphanFailure.Retain(failure, owner.Cleanup.Errors);
    }
}
