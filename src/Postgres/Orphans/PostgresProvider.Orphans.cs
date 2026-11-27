using System.Data;
using System.Data.Common;
using Npgsql;

namespace Neemle.OrmLite.Postgres;

public sealed partial class PostgresProvider : IOrphanSnapshotGuard
{
    public async Task<IOrphanGuardLease> AcquireAsync(DbConnection connection, DbTransaction transaction,
        OrphanGuardRequest request, CancellationToken ct)
    {
        try
        {
            OrphanGuardSession.ValidateInputs(connection, transaction, request);
            if (connection is not NpgsqlConnection || transaction is not NpgsqlTransaction
                || transaction.IsolationLevel != IsolationLevel.ReadCommitted)
                throw OrphanRefusal.At(OrphanError.UnsupportedGuard);
            var session = new OrphanGuardSession(connection, transaction, request);
            var catalog = new PostgresOrphanCatalog(this, session);
            var tables = await catalog.AcquireAsync(ct);
            var keys = await OrphanGuardKeys.ObserveAsync(this, session, " FOR UPDATE", tables, ct);
            return new PostgresOrphanLease(session, catalog, keys);
        }
        catch (Exception error) { throw OrphanGuardFailure.Safe(error, ct); }
    }
}
