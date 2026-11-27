namespace Neemle.OrmLite.Postgres;

internal sealed class PostgresOrphanCatalog(PostgresProvider provider, OrphanGuardSession session)
{
    private PostgresOrphanTable? _child;

    internal async Task<OrphanGuardTables> AcquireAsync(CancellationToken ct)
    {
        var schemas = await session.ReadAsync(new("SELECT pg_catalog.current_schemas(true)", [], StatementKind.Read),
            r => r.GetFieldValue<string[]>(0), 1, OrphanError.UnverifiedEffects, ct);
        if (schemas.Count != 1 || schemas[0].FirstOrDefault() != "pg_catalog")
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        var parent = await ResolveAsync(session.Shape.Parent.Table, ct);
        var child = await ResolveAsync(session.Shape.Child.Table, ct);
        await LockAsync(parent, ct);
        await LockAsync(child, ct);
        await ColumnsAsync(session.Shape.Parent, parent, ct);
        await ColumnsAsync(session.Shape.Child, child, ct);
        await OwnerAsync(parent, child, ct);
        _child = child;
        return new(parent.Sql, child.Sql);
    }

    internal async Task<PostgresOrphanTable> ResolveAsync(string table, CancellationToken ct)
    {
        var sql = "SELECT c.oid::bigint,pg_catalog.format('%I.%I',n.nspname,c.relname), "
            + "c.relkind='r' AND c.relpersistence='p' AND NOT c.relrowsecurity AND NOT c.relhasrules "
            + "AND NOT EXISTS(SELECT 1 FROM pg_catalog.pg_inherits i "
            + "WHERE i.inhrelid=c.oid OR i.inhparent=c.oid) "
            + "AND pg_catalog.current_setting('session_replication_role')='origin' "
            + "FROM pg_catalog.pg_class c JOIN pg_catalog.pg_namespace n ON n.oid=c.relnamespace "
            + "WHERE c.oid=pg_catalog.to_regclass(@owner)";
        var rows = await session.ReadAsync(OrphanGuardSql.Bound(sql, OrphanGuardSql.Name(provider, table)),
            r => new PostgresOrphanTable(r.GetInt64(0), r.GetString(1), r.GetBoolean(2)),
            1, OrphanError.UnverifiedEffects, ct);
        if (rows.Count != 1 || !rows[0].Ordinary) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        return rows[0];
    }

    internal Task LockAsync(PostgresOrphanTable table, CancellationToken ct) =>
        session.ExecuteAsync("LOCK TABLE ONLY " + table.Sql + " IN SHARE UPDATE EXCLUSIVE MODE", ct);

    private async Task ColumnsAsync(EntityDescriptor entity, PostgresOrphanTable table, CancellationToken ct)
    {
        var sql = "SELECT a.attname,pg_catalog.format_type(a.atttypid,a.atttypmod),NOT a.attnotnull, "
            + "EXISTS(SELECT 1 FROM pg_catalog.pg_constraint k WHERE k.conrelid=a.attrelid "
            + "AND k.contype='p' AND k.conkey=ARRAY[a.attnum]), "
            + "a.atthasdef OR a.attidentity<>'' OR a.attgenerated<>'' "
            + "FROM pg_catalog.pg_attribute a WHERE a.attrelid=@owner::pg_catalog.oid AND a.attnum>0 "
            + "AND NOT a.attisdropped ORDER BY a.attnum LIMIT 257";
        var columns = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid),
            r => new OrphanGuardColumn(r.GetString(0), r.GetString(1), r.GetBoolean(2), r.GetBoolean(3),
                r.GetBoolean(4)), 256, OrphanError.UnverifiedEffects, ct);
        OrphanGuardColumns.Validate(provider, entity, columns);
        var current = await ResolveAsync(entity.Table, ct);
        if (current != table) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task OwnerAsync(PostgresOrphanTable parent, PostgresOrphanTable child, CancellationToken ct)
    {
        var sql = "SELECT c.confrelid::bigint,p.attname FROM pg_catalog.pg_constraint c "
            + "JOIN pg_catalog.pg_attribute a ON a.attrelid=c.conrelid AND c.conkey=ARRAY[a.attnum] "
            + "JOIN pg_catalog.pg_attribute p ON p.attrelid=c.confrelid AND c.confkey=ARRAY[p.attnum] "
            + "WHERE c.conrelid=@owner::pg_catalog.oid AND c.contype='f' AND c.convalidated "
            + "AND a.attname=@column LIMIT 2";
        var query = new Statement(sql, [new(0, "owner", child.Oid), new(1, "column", session.Shape.Owner.Column)],
            StatementKind.Read);
        var rows = await session.ReadAsync(query, r => (Oid: r.GetInt64(0), Column: r.GetString(1)),
            1, OrphanError.UnsupportedMapping, ct);
        if (rows.Count != 1 || rows[0].Oid != parent.Oid || rows[0].Column != session.Shape.Parent.SoleKey!.Column)
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
    }

    internal async Task VerifyAsync(OrphanMode mode, CancellationToken ct)
    {
        var child = _child ?? throw OrphanRefusal.At(OrphanError.ScopeEnded);
        await IncomingAsync(child, ct);
        await PlainEffectsAsync(child, session.Shape.Child.SoftColumn, ct);
        await new PostgresOrphanHistory(provider, session, this).VerifyAsync(child, mode, ct);
    }

    internal async Task IncomingAsync(PostgresOrphanTable table, CancellationToken ct)
    {
        var sql = "SELECT confdeltype::pg_catalog.text,confupdtype::pg_catalog.text "
            + "FROM pg_catalog.pg_constraint WHERE confrelid=@owner::pg_catalog.oid AND contype='f' LIMIT 257";
        var rows = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid),
            r => (Delete: r.GetString(0), Update: r.GetString(1)), 256, OrphanError.UnverifiedEffects, ct);
        if (rows.Any(r => !Safe(r.Delete) || !Safe(r.Update)))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private static bool Safe(string action) => action is "a" or "r";

    internal async Task PlainEffectsAsync(PostgresOrphanTable table, string? soft, CancellationToken ct)
    {
        var checks = await session.ReadAsync(OrphanGuardSql.Bound("SELECT 1 FROM pg_catalog.pg_constraint "
            + "WHERE conrelid=@owner::pg_catalog.oid AND contype='c' LIMIT 1", table.Oid), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (checks.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        await IndexEffectsAsync(table, soft, ct);
    }

    internal async Task IndexEffectsAsync(PostgresOrphanTable table, string? soft, CancellationToken ct)
    {
        var sql = "SELECT EXISTS(SELECT 1 FROM pg_catalog.pg_index i WHERE i.indrelid=@owner::pg_catalog.oid AND "
            + "(i.indexprs IS NOT NULL OR (i.indpred IS NOT NULL AND "
            + "pg_catalog.pg_get_expr(i.indpred,i.indrelid) IS DISTINCT FROM "
            + "'('||pg_catalog.quote_ident(@soft)||' IS NULL)'))) "
            + "OR EXISTS(SELECT 1 FROM pg_catalog.pg_index i,"
            + "LATERAL pg_catalog.unnest(i.indclass::pg_catalog.oid[]) classes(opclass) "
            + "JOIN pg_catalog.pg_opclass o ON o.oid=classes.opclass "
            + "JOIN pg_catalog.pg_namespace n ON n.oid=o.opcnamespace "
            + "WHERE i.indrelid=@owner::pg_catalog.oid AND n.nspname<>'pg_catalog')";
        var query = new Statement(sql, [new(0, "owner", table.Oid), new(1, "soft", soft)], StatementKind.Read);
        var rows = await session.ReadAsync(query, r => r.GetBoolean(0), 1, OrphanError.UnverifiedEffects, ct);
        if (rows.Single()) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }
}
