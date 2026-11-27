namespace Neemle.OrmLite;

/// <summary>Turns any failure inside a guard into the replacement's safe error contract.</summary>
public static class OrphanGuardFailure
{
    /// <summary>
    /// Returns an orphan exception unchanged; turns a cancellation of <paramref name="ct"/> into
    /// <see cref="OrphanCanceledException"/>, and any other exception into an <see cref="OrphanException"/> with
    /// <see cref="OrphanError.ProviderFailure"/>. Both carry <see cref="OrphanOutcome.NotStarted"/> and keep the
    /// original only as the inner exception.
    /// </summary>
    /// <param name="error">The failure.</param>
    /// <param name="ct">The guard's cancellation token.</param>
    /// <returns>The exception the guard throws.</returns>
    public static Exception Safe(Exception error, CancellationToken ct) => error switch
    {
        OrphanException or OrphanCanceledException => error,
        OperationCanceledException when ct.IsCancellationRequested =>
            new OrphanCanceledException(OrphanOutcome.NotStarted, ct, error),
        _ => new OrphanException(OrphanError.ProviderFailure, OrphanOutcome.NotStarted, error),
    };
}
