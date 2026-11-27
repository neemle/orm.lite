using System.Data.Common;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteProvider : IOrphanSnapshotGuard
{
    public async Task<IOrphanGuardLease> AcquireAsync(DbConnection connection, DbTransaction transaction,
        OrphanGuardRequest request, CancellationToken ct)
    {
        try
        {
            OrphanGuardSession.ValidateInputs(connection, transaction, request);
            if (connection is not OrmSqliteConnection native || !native.OwnsWriteTransaction(transaction))
                throw OrphanRefusal.At(OrphanError.UnsupportedGuard);
            var session = new OrphanGuardSession(connection, transaction, request);
            var catalog = new SqliteOrphanCatalog(this, session);
            await catalog.AcquireAsync(ct);
            var tables = new OrphanGuardTables("main." + OrphanGuardSql.Name(this, request.Parent.Table),
                "main." + OrphanGuardSql.Name(this, request.Child.Table));
            var keys = await OrphanGuardKeys.ObserveAsync(this, session, "", tables, ct);
            return new SqliteOrphanLease(session, catalog, keys);
        }
        catch (Exception error) { throw OrphanGuardFailure.Safe(error, ct); }
    }
}
