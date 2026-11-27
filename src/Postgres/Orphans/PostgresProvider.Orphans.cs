using System.Data.Common;
using System.Data;
using Neemle.OrmLite.Sql;
using Npgsql;
using L = Neemle.OrmLite.Postgres.PostgresOrphanDataLiterals;

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
            var keys = await OrphanGuardKeys.ObserveAsync(session,
                OrphanGuardSql.Parent(this, session.Shape, L.RowLockSuffix, tables),
                OrphanGuardSql.Children(this, session.Shape, L.RowLockSuffix, tables), ct);
            return new PostgresOrphanLease(session, catalog, keys);
        }
        catch (Exception error) { throw OrphanGuardFailure.Safe(error, ct); }
    }
}
