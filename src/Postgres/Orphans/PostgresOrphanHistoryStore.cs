using Neemle.OrmLite.Sql;
using L = Neemle.OrmLite.Postgres.PostgresOrphanDataLiterals;

namespace Neemle.OrmLite.Postgres;

internal sealed class PostgresOrphanHistoryStore(OrphanGuardSession session, PostgresOrphanCatalog catalog)
{
    internal async Task VerifyAsync(PostgresOrphanTable table, CancellationToken ct)
    {
        var sql = L.HistoryStoreVerify;
        var columns = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid),
            r => (Name: r.GetString(0), Type: r.GetString(1), Required: r.GetBoolean(2), Generated: r.GetString(3)),
            10, OrphanError.UnverifiedEffects, ct);
        if (!columns.SequenceEqual(Columns)) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        await KeyAsync(table, ct);
        await ChecksAsync(table, ct);
        await catalog.IncomingAsync(table, ct);
        await catalog.IndexEffectsAsync(table, null, ct);
        var triggers = await session.ReadAsync(OrphanGuardSql.Bound(L.HistoryStoreVerify2, table.Oid),
            r => r.GetInt32(0),
            1, OrphanError.UnverifiedEffects, ct);
        if (triggers.Count != 0) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task ChecksAsync(PostgresOrphanTable table, CancellationToken ct)
    {
        var sql = L.HistoryStoreChecks;
        var checks = await session.ReadAsync(OrphanGuardSql.Bound(sql, table.Oid), r => r.GetString(0),
            2, OrphanError.UnverifiedEffects, ct);
        if (checks.Count != 2 || checks.Distinct(StringComparer.Ordinal).Count() != 2
            || checks.Any(c => !AllowedChecks.Contains(c, StringComparer.Ordinal)))
            throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
    }

    private async Task KeyAsync(PostgresOrphanTable table, CancellationToken ct)
    {
        var sql = L.HistoryStoreKey;
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
        ("xact_id", "bigint", true, "||"), ("changed_at", L.HistoryStoreTimestampType, true, L.HistoryStoreNowDefault),
    ];

    private static readonly string[] AllowedChecks =
    [
        L.HistoryStoreOpCheck,
        L.HistoryStoreActorCheck,
    ];
}
