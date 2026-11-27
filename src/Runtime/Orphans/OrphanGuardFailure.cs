namespace Neemle.OrmLite;

public static class OrphanGuardFailure
{
    public static Exception Safe(Exception error, CancellationToken ct) => error switch
    {
        OrphanException or OrphanCanceledException => error,
        OperationCanceledException when ct.IsCancellationRequested =>
            new OrphanCanceledException(OrphanOutcome.NotStarted, ct, error),
        _ => new OrphanException(OrphanError.ProviderFailure, OrphanOutcome.NotStarted, error),
    };
}
