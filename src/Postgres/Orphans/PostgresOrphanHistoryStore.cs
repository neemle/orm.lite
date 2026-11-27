namespace Neemle.OrmLite.Postgres;

internal sealed class PostgresOrphanHistoryStore(OrphanGuardSession session, PostgresOrphanCatalog catalog)
{
    internal async Task VerifyAsync(PostgresOrphanTable table, CancellationToken ct)
    {
        var sql = "SELECT a.attname,pg_catalog.format_type(a.atttypid,a.atttypmod),a.attnotnull,"
            + "a.attidentity::pg_catalog.text||'|'||a.attgenerated::pg_catalog.text||'|'||"
            + "coalesce(pg_catalog.pg_get_expr(d.adbin,d.adrelid),'') "
            + "FROM pg_catalog.pg_attribute a LEFT JOIN pg_catalog.pg_attrdef d "
            + "ON d.adrelid=a.attrelid AND d.adnum=a.attnum WHERE a.attrelid=@owner::pg_catalog.oid "
            + "AND a.attnum>0 AND NOT a.attisdropped ORDER BY a.attnum LIMIT 12";
        var columns = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid),
            r => (Name: r.GetString(0), Type: r.GetString(1), Required: r.GetBoolean(2), Generated: r.GetString(3)),
            10, OrphanError.UnverifiedEffects, ct);
        if (!columns.SequenceEqual(Columns)) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        await KeyAsync(table, ct);
        await ChecksAsync(table, ct);
        await catalog.IncomingAsync(table, ct);
        await catalog.IndexEffectsAsync(table, null, ct);
        var triggers = await session.ReadAsync(OrphanGuardSql.Bound("SELECT 1 FROM pg_catalog.pg_trigger "
            + "WHERE tgrelid=@owner::pg_catalog.oid LIMIT 1", table.Oid), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (triggers.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task ChecksAsync(PostgresOrphanTable table, CancellationToken ct)
    {
        var sql = "SELECT pg_catalog.pg_get_constraintdef(oid) FROM pg_catalog.pg_constraint "
            + "WHERE conrelid=@owner::pg_catalog.oid AND contype='c' ORDER BY conname LIMIT 4";
        var checks = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid), r => r.GetString(0),
            2, OrphanError.UnverifiedEffects, ct);
        if (checks.Count != 2 || checks.Distinct(StringComparer.Ordinal).Count() != 2
            || checks.Any(c => !AllowedChecks.Contains(c, StringComparer.Ordinal)))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task KeyAsync(PostgresOrphanTable table, CancellationToken ct)
    {
        var sql = "SELECT 1 FROM pg_catalog.pg_constraint c JOIN pg_catalog.pg_attribute a "
            + "ON a.attrelid=c.conrelid AND c.conkey=ARRAY[a.attnum] WHERE c.conrelid=@owner::pg_catalog.oid "
            + "AND c.contype='p' AND c.convalidated AND a.attname='id'";
        var keys = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid), r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (keys.Count != 1) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private static readonly (string Name, string Type, bool Required, string Generated)[] Columns =
    [
        ("id", "bigint", true, "a||"), ("tenant_id", "uuid", false, "||"),
        ("source_table", "text", true, "||"), ("row_id", "uuid", true, "||"),
        ("op", "text", true, "||"), ("old_values", "jsonb", true, "||"),
        ("actor_user_id", "uuid", false, "||"), ("actor_kind", "text", true, "||"),
        ("xact_id", "bigint", true, "||"), ("changed_at", "timestamp with time zone", true, "||now()"),
    ];

    private static readonly string[] AllowedChecks =
    [
        "CHECK ((op = ANY (ARRAY['update'::text, 'soft_delete'::text, 'restore'::text])))",
        "CHECK ((actor_kind = ANY (ARRAY['user'::text, 'system'::text, 'unknown'::text])))",
    ];
}
