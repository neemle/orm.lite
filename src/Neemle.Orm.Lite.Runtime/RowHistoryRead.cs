#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Neemle.Orm.Lite;

public sealed record RowHistoryVersion(long Id, string Op, Guid? ActorUserId, string ActorKind, string[] ChangedKeys, long XactId, DateTime ChangedAt);

public static class RowHistoryRead
{
    private static readonly Regex Identifier = new(@"\A[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

    private const string VersionsSql =
        "SELECT id, op, actor_user_id, actor_kind, ARRAY(SELECT k FROM jsonb_object_keys(old_values) AS k ORDER BY k), xact_id, changed_at"
        + " FROM row_history WHERE source_table = @t AND row_id = @id";

    public static async Task<bool> RowExistsAsync(
        OrmContext ctx, string sourceTable, Guid rowId, Guid? tenantScope = null, CancellationToken ct = default)
    {
        var meta = Resolve(sourceTable);
        var keyColumn = KeyOf(meta, sourceTable);
        var tenantColumn = tenantScope is null ? null : ScopeColumn(meta);
        await using var cmd = ctx.DataSource.CreateCommand(RenderExistsSql(sourceTable, keyColumn, tenantColumn));
        cmd.AddParam("id", rowId);
        if (tenantColumn is not null) cmd.AddParam("w", tenantScope!.Value);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public static async Task<IReadOnlyList<RowHistoryVersion>> VersionsAsync(
        OrmContext ctx, string sourceTable, Guid rowId, Guid? tenantScope = null, CancellationToken ct = default)
    {
        var meta = Resolve(sourceTable);
        var tenantColumn = tenantScope is null ? null : ScopeColumn(meta);
        await using var cmd = ctx.DataSource.CreateCommand(
            RenderVersionsSql(sourceTable, tenantColumn is null ? null : KeyOf(meta, sourceTable), tenantColumn));
        cmd.AddParam("t", sourceTable);
        cmd.AddParam("id", rowId);
        if (tenantColumn is not null) cmd.AddParam("w", tenantScope!.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var versions = new List<RowHistoryVersion>();
        while (await reader.ReadAsync(ct))
            versions.Add(new RowHistoryVersion(
                reader.GetFieldValue<long>(0),
                reader.GetFieldValue<string>(1),
                await reader.IsDBNullAsync(2, ct) ? null : reader.GetFieldValue<Guid>(2),
                reader.GetFieldValue<string>(3),
                reader.GetFieldValue<string[]>(4),
                reader.GetFieldValue<long>(5),
                DateTime.SpecifyKind(reader.GetFieldValue<DateTime>(6), DateTimeKind.Utc)));
        return versions;
    }

    public static async Task<JsonDocument?> StateOfVersionAsync(
        OrmContext ctx, string sourceTable, Guid rowId, long versionId, Guid? tenantScope = null, CancellationToken ct = default)
    {
        var meta = Resolve(sourceTable);
        var keyColumn = KeyOf(meta, sourceTable);
        var tenantColumn = tenantScope is null ? null : ScopeColumn(meta);
        await using var cmd = ctx.DataSource.CreateCommand(RenderStateSql(sourceTable, keyColumn, meta.Unversioned ?? [], tenantColumn));
        cmd.AddParam("t", sourceTable);
        cmd.AddParam("id", rowId);
        cmd.AddParam("k", versionId);
        if (tenantColumn is not null) cmd.AddParam("w", tenantScope!.Value);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return JsonDocument.Parse(reader.GetFieldValue<string>(0));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string RenderStateSql(string table, string keyColumn, IReadOnlyList<string> unversioned, string? tenantColumn = null)
    {
        foreach (var name in new[] { table, keyColumn }.Concat(unversioned).Concat(tenantColumn is null ? [] : [tenantColumn]))
            if (!Identifier.IsMatch(name))
                throw new ArgumentException("not an identifier: " + name);
        var stripped = unversioned.Count == 0
            ? "ARRAY[]::text[]"
            : "ARRAY[" + string.Join(", ", unversioned.Select(c => "'" + c + "'")) + "]::text[]";
        return "SELECT (to_jsonb(live.*) || coalesce((SELECT jsonb_object_agg(x.k, x.v) FROM"
            + " (SELECT DISTINCT ON (e.k) e.k, e.v FROM row_history h CROSS JOIN LATERAL jsonb_each(h.old_values) AS e(k, v)"
            + " WHERE h.source_table = @t AND h.row_id = @id AND h.id >= @k ORDER BY e.k, h.id) x), '{}'::jsonb)) - " + stripped
            + " FROM \"" + table + "\" live WHERE live.\"" + keyColumn + "\" = @id"
            + (tenantColumn is null ? "" : " AND live.\"" + tenantColumn + "\" = @w")
            + " AND EXISTS (SELECT 1 FROM row_history v WHERE v.id = @k AND v.source_table = @t AND v.row_id = @id)";
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string RenderVersionsSql(string table, string? keyColumn, string? tenantColumn)
    {
        if (tenantColumn is null) return VersionsSql + " ORDER BY id DESC";
        foreach (var name in new[] { table, keyColumn ?? "", tenantColumn })
            if (!Identifier.IsMatch(name))
                throw new ArgumentException("not an identifier: " + name);
        return VersionsSql + " AND EXISTS (SELECT 1 FROM \"" + table + "\" r WHERE r.\"" + keyColumn + "\" = @id AND r.\"" + tenantColumn + "\" = @w)"
            + " ORDER BY id DESC";
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string RenderExistsSql(string table, string keyColumn, string? tenantColumn)
    {
        foreach (var name in new[] { table, keyColumn }.Concat(tenantColumn is null ? [] : [tenantColumn]))
            if (!Identifier.IsMatch(name))
                throw new ArgumentException("not an identifier: " + name);
        return "SELECT EXISTS (SELECT 1 FROM \"" + table + "\" r WHERE r.\"" + keyColumn + "\" = @id"
            + (tenantColumn is null ? "" : " AND r.\"" + tenantColumn + "\" = @w") + ")";
    }

    private static string KeyOf(SoftDeleteMeta meta, string sourceTable) =>
        meta.KeyColumn ?? throw new InvalidOperationException("no key column registered for " + sourceTable);

    private static string? ScopeColumn(SoftDeleteMeta meta) => ScopeColumn(meta.Platform, meta.TenantColumn, meta.KeyColumn);

    internal static string? ScopeColumn(bool platform, string? tenantColumn, string? keyColumn) =>
        platform || tenantColumn is null || string.Equals(tenantColumn, keyColumn, StringComparison.Ordinal)
            ? null
            : tenantColumn;

    private static SoftDeleteMeta Resolve(string sourceTable)
    {
        foreach (var entry in OrmMeta.SoftEntries())
            if (string.Equals(entry.Table, sourceTable, StringComparison.Ordinal))
                return entry.Meta;
        throw new ArgumentException("not a registered soft table: " + sourceTable, nameof(sourceTable));
    }
}
