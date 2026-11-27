using System.Data.Common;

namespace Neemle.OrmLite;

public interface IOrphanSnapshotGuard
{
    Task<IOrphanGuardLease> AcquireAsync(DbConnection connection, DbTransaction transaction,
        OrphanGuardRequest request, CancellationToken ct);
}
