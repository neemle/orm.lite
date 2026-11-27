namespace Neemle.OrmLite;

/// <summary>
/// The observation an <see cref="IOrphanSnapshotGuard"/> took: the original child keys, the statements the guard
/// spent, and the check that the database adds no effect the replacement cannot see. Disposing it ends the guard's
/// session; it does not end the transaction, which the replacement owns.
/// </summary>
public interface IOrphanGuardLease : IAsyncDisposable
{
    /// <summary>The primary keys of the LIVE children owned by the parent when the guard observed them.</summary>
    IReadOnlyList<object> OriginalKeys { get; }

    /// <summary>
    /// The statements the guard has run so far; they count toward <see cref="OrphanLimits.MaxStatements"/>.
    /// </summary>
    int StatementsUsed { get; }

    /// <summary>
    /// Checks, before the first user write, that the chosen <paramref name="mode"/> fits the child mapping and that no
    /// trigger, incoming foreign-key action or other catalog object would add an effect beyond the exact declared rows.
    /// </summary>
    /// <param name="mode">The orphan mode the replacement will apply.</param>
    /// <param name="ct">Cancels the check.</param>
    /// <exception cref="OrphanException">An unverified effect, mode or mapping refusal.</exception>
    /// <exception cref="OrphanCanceledException"><paramref name="ct"/> was canceled.</exception>
    Task VerifyEffectsAsync(OrphanMode mode, CancellationToken ct);
}
