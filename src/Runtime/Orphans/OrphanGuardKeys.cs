using System.Data.Common;

namespace Neemle.OrmLite;

/// <summary>Reads the parent row and its owned children through a guard session.</summary>
public static class OrphanGuardKeys
{
    /// <summary>
    /// Runs the parent read, which must return the parent key, then the children read, which must return each child
    /// key and its owner key. Every key is decoded by the registered typed binding and compared exactly, so a database
    /// collation cannot widen the parent or the owned membership.
    /// </summary>
    /// <param name="session">The guard session.</param>
    /// <param name="parentRead">Reads the parent key, at most one row.</param>
    /// <param name="childrenRead">Reads child key and owner key, in that order.</param>
    /// <param name="ct">Cancels the reads.</param>
    /// <returns>The original child keys.</returns>
    /// <exception cref="OrphanException"><see cref="OrphanError.ParentMissing"/>,
    /// <see cref="OrphanError.ParentMismatch"/>, <see cref="OrphanError.OwnershipChanged"/>, or
    /// <see cref="OrphanError.LimitExceeded"/> when more than <see cref="OrphanLimits.MaxObserved"/> children
    /// return.</exception>
    public static async Task<IReadOnlyList<object>> ObserveAsync(OrphanGuardSession session, Statement parentRead,
        Statement childrenRead, CancellationToken ct)
    {
        if (session is null || parentRead is null || childrenRead is null)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        var shape = session.Shape;
        var parent = await session.ReadAsync(parentRead,
            r => shape.ParentBinding.ReadKey(r, 0), 1, OrphanError.UnsupportedMapping, ct);
        if (parent.Count == 0) throw OrphanRefusal.At(OrphanError.ParentMissing);
        if (!shape.ParentBinding.KeyComparer.Equals(parent[0], shape.Request.ParentKey))
            throw OrphanRefusal.At(OrphanError.ParentMismatch);
        return await session.ReadAsync(childrenRead,
            r => Child(r, shape), shape.Request.Limits.MaxObserved,
            OrphanError.LimitExceeded, ct);
    }

    private static object Child(DbDataReader reader, OrphanGuardShape shape)
    {
        var owner = shape.ParentBinding.ReadKey(reader, 1);
        if (!shape.ParentBinding.KeyComparer.Equals(owner, shape.Request.ParentKey))
            throw OrphanRefusal.At(OrphanError.OwnershipChanged);
        return shape.ChildBinding.ReadKey(reader, 0);
    }
}
