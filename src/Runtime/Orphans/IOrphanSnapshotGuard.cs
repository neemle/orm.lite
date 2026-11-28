using System.Data.Common;

namespace Neemle.OrmLite;

/// <summary>
/// Optional provider capability behind <see cref="OrphanCollections"/>: it observes the complete LIVE membership of
/// one owned collection inside the replacement's own transaction and keeps that membership stable until the
/// transaction ends. A provider that does not implement it cannot run an orphan replacement; the operation refuses
/// with <see cref="OrphanError.UnsupportedGuard"/> before any I/O.
/// </summary>
/// <remarks>
/// The PostgreSQL and SQLite providers implement this interface. It is a provider extension point, not an
/// application API: applications call <see cref="OrphanCollections"/> and never acquire a guard themselves.
/// </remarks>
public interface IOrphanSnapshotGuard
{
    /// <summary>
    /// Verifies the parent and child tables, locks or otherwise protects the parent row and its owned children, and
    /// reads the original child keys, all on <paramref name="connection"/> inside <paramref name="transaction"/>.
    /// </summary>
    /// <param name="connection">The open connection the replacement owns; the guard never opens or closes it.</param>
    /// <param name="transaction">The replacement's transaction on <paramref name="connection"/>.</param>
    /// <param name="request">The validated parent, child, owner foreign key, parent key and limits.</param>
    /// <param name="ct">Cancels the observation.</param>
    /// <returns>A lease that holds the observation until it is disposed.</returns>
    /// <exception cref="OrphanException">The guard refused, with <see cref="OrphanOutcome.NotStarted"/>.</exception>
    /// <exception cref="OrphanCanceledException"><paramref name="ct"/> was canceled.</exception>
    Task<IOrphanGuardLease> AcquireAsync(DbConnection connection, DbTransaction transaction,
        OrphanGuardRequest request, CancellationToken ct);
}
