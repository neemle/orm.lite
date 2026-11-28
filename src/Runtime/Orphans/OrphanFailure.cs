namespace Neemle.OrmLite;

internal sealed record OrphanFailure(OrphanError Error, Exception Cause, bool Canceled, CancellationToken Token)
{
    internal static OrphanFailure Observe(Exception primary, CancellationToken token)
    {
        var cause = primary.InnerException ?? primary;
        var error = primary is OrphanException typed ? typed.Error : OrphanError.ProviderFailure;
        return new(error, primary is OrphanException or OrphanCanceledException ? cause : primary,
            primary is OperationCanceledException && token.IsCancellationRequested, token);
    }

    internal Exception At(OrphanOutcome outcome, OrphanError? error = null, OrphanResult? result = null)
    {
        if (Canceled && error is null && outcome is OrphanOutcome.NotStarted or OrphanOutcome.RolledBack)
            return new OrphanCanceledException(outcome, Token, Cause);
        return new OrphanException(error ?? Error, outcome, Cause, result);
    }

    internal static Exception Retain(Exception primary, IReadOnlyList<Exception> cleanup)
    {
        foreach (var error in cleanup) PolicyCleanup.Retain(primary, error);
        return primary;
    }
}
