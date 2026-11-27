namespace Neemle.OrmLite.Sqlite;

internal sealed class SqliteOrphanHistory(SqliteProvider provider, OrphanGuardSession session,
    SqliteOrphanCatalog catalog)
{
    internal async Task VerifyAsync(IReadOnlyList<SqliteOrphanObject> objects, OrphanMode mode, CancellationToken ct)
    {
        var triggers = objects.Where(o => o.Type == "trigger").ToArray();
        if (triggers.Length == 0)
        {
            if (session.Shape.Child.Soft is not null) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
            return;
        }
        if (mode == OrphanMode.Delete) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        var entity = session.Shape.Child;
        var history = entity.HistoryTable() ?? throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        var spec = new SqliteHistorySpec(history.KeyColumn, history.SoftColumn, null, history.Unversioned);
        var expected = spec.Triggers(entity.Table, entity.ColumnNames)
            .Select(n => SqliteOrphanDefinitions.Text(provider, n)).ToArray();
        if (triggers.Length != expected.Length || triggers.Any(t =>
            !expected.Contains(t.Sql.Trim().TrimEnd(';'), StringComparer.Ordinal)))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        await StoreAsync(ct);
    }

    private async Task StoreAsync(CancellationToken ct)
    {
        var objects = await catalog.ObjectsAsync(RowHistoryDdl.TableName, ct);
        var expected = SqliteOrphanDefinitions.Text(provider, new SqliteHistoryStore())
            .Replace("IF NOT EXISTS ", "", StringComparison.Ordinal);
        var table = objects.SingleOrDefault(o => o.Type == "table");
        if (table?.Sql.Trim().TrimEnd(';') != expected || objects.Any(o => o.Type == "trigger"))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        foreach (var index in objects.Where(o => o.Type == "index")) await IndexAsync(index, ct);
        await catalog.IncomingAsync(RowHistoryDdl.TableName, ct);
        var temporary = await session.ReadAsync(OrphanGuardSql.Bound("SELECT 1 FROM temp.sqlite_schema "
            + "WHERE tbl_name=@owner LIMIT 1", RowHistoryDdl.TableName), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (temporary.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task IndexAsync(SqliteOrphanObject index, CancellationToken ct)
    {
        var query = "SELECT cid,coll,(SELECT partial FROM pragma_index_list('row_history','main') "
            + "WHERE name=@owner) FROM ";
        var columns = await session.ReadAsync(OrphanGuardSql.Bound(query
            + "pragma_index_xinfo(@owner,'main') WHERE key=1 LIMIT 257", index.Name),
            r => (Column: r.GetInt32(0), Collation: r.GetString(1), Partial: r.GetInt32(2)),
            256, OrphanError.UnverifiedEffects, ct);
        if (columns.Any(c => c.Column < 0 || c.Collation != "BINARY" || c.Partial != 0))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }
}
