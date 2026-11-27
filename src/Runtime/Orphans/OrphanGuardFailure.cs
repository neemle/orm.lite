namespace Neemle.OrmLite;

internal static class OrphanGuardFailure
{
    internal static Exception Safe(Exception error, CancellationToken ct) => error switch
    {
        OrphanException or OrphanCanceledException => error,
        OperationCanceledException when ct.IsCancellationRequested =>
            new OrphanCanceledException(OrphanOutcome.NotStarted, ct, error),
        _ => new OrphanException(OrphanError.ProviderFailure, OrphanOutcome.NotStarted, error),
    };
}
