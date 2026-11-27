using System.Data.Common;
using Neemle.OrmLite.Sql;
using L = Neemle.OrmLite.Sqlite.SqliteOrphanDataLiterals;

namespace Neemle.OrmLite.Sqlite;

internal sealed class SqliteOrphanCatalog(SqliteProvider provider, OrphanGuardSession session)
{
    internal async Task AcquireAsync(CancellationToken ct)
    {
        var enabled = await session.ReadAsync(new(L.CatalogAcquire, [], StatementKind.Read),
            r => r.GetInt32(0), 1, OrphanError.UnverifiedEffects, ct);
        if (enabled.SingleOrDefault() != 1) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        await ColumnsAsync(session.Shape.Parent, ct);
        await ColumnsAsync(session.Shape.Child, ct);
        await RefuseTempAsync(session.Shape.Parent.Table, ct);
        await RefuseTempAsync(session.Shape.Child.Table, ct);
    }

    private async Task ColumnsAsync(EntityDescriptor entity, CancellationToken ct)
    {
        var query = OrphanGuardSql.Bound(L.CatalogColumns, entity.Table);
        var columns = await session.ReadAsync(query, r => ReadColumn(entity, r),
            256, OrphanError.UnverifiedEffects, ct);
        OrphanGuardColumns.Validate(provider, entity, columns);
    }

    private static OrphanGuardColumn ReadColumn(EntityDescriptor entity, DbDataReader reader)
    {
        SqliteOrphanDefaults.Validate(entity, reader.GetString(0), reader.IsDBNull(5) ? null : reader.GetString(5));
        return new(reader.GetString(0), reader.GetString(1), reader.GetInt32(2) == 0 && reader.GetInt32(3) == 0,
            reader.GetInt32(3) != 0, reader.GetInt32(4) != 0);
    }

    internal async Task<IReadOnlyList<SqliteOrphanObject>> ObjectsAsync(string table, CancellationToken ct) =>
        await session.ReadAsync(OrphanGuardSql.Bound(L.CatalogObjects, table),
            r => new SqliteOrphanObject(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3)),
            256, OrphanError.UnverifiedEffects, ct);

    private async Task RefuseTempAsync(string table, CancellationToken ct)
    {
        var found = await session.ReadAsync(OrphanGuardSql.Bound(L.CatalogRefuseTemp, table), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (found.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    internal async Task VerifyAsync(OrphanMode mode, CancellationToken ct)
    {
        var shape = session.Shape;
        var objects = await ObjectsAsync(shape.Child.Table, ct);
        SqliteOrphanDefinitions.Table(provider, shape.Child, objects);
        SqliteOrphanDefinitions.Indexes(provider, shape.Child, objects);
        await IncomingAsync(shape.Child.Table, ct);
        await new SqliteOrphanHistory(provider, session, this).VerifyAsync(objects, mode, ct);
    }

    internal async Task IncomingAsync(string table, CancellationToken ct)
    {
        var sql = L.CatalogIncoming;
        var effects = await session.ReadAsync(OrphanGuardSql.Bound(sql, table),
            r => (Delete: r.GetString(0), Update: r.GetString(1)), 256, OrphanError.UnverifiedEffects, ct);
        if (effects.Any(e => !Safe(e.Delete) || !Safe(e.Update)))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private static bool Safe(string action) => action is L.CatalogSafe or "RESTRICT";
}
