using System.Data.Common;

namespace Neemle.OrmLite;

public static class OrphanGuardKeys
{
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
