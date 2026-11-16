#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Neemle.Orm.Lite;

public sealed record SoftTrashFilter(string? EntityToken = null, Guid? ProjectId = null, bool IncludePlatform = false);

public sealed record SoftTrashRow(
    string EntityToken, string Table, Guid Id, string? Label, Guid? ProjectId, DateTime DeletedAt,
    Guid? ActorUserId, string? ActorKind, long? XactId, DateTime? ChangedAt);

public sealed record SoftTrashPage(IReadOnlyList<SoftTrashRow> Rows, bool HasMore, IReadOnlyList<string> SkippedTables);

public static class SoftTrashRead
{
    public const int LabelMaxChars = 200;

    private static readonly Regex Identifier = new(@"\A[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant);

    private static readonly ConditionalWeakTable<DbDataSource, ConcurrentDictionary<string, bool>> Present = new();

    private const string ExistingTablesSql =
        "SELECT t FROM unnest(@tables::text[]) AS t WHERE to_regclass(quote_ident(t)) IS NOT NULL";

    private const string SoftDeleteBatchSql =
        "SELECT p.t, p.id, h.actor_user_id, h.actor_kind, h.xact_id, h.changed_at FROM unnest(@tables::text[], @ids::uuid[]) AS p(t, id)"
        + " CROSS JOIN LATERAL (SELECT actor_user_id, actor_kind, xact_id, changed_at FROM row_history x WHERE x.source_table = p.t"
        + " AND x.row_id = p.id AND x.op = 'soft_delete' ORDER BY x.id DESC LIMIT 1) h";

    public static async Task<SoftTrashPage> ListAsync(
        OrmContext ctx, IReadOnlyList<ISoftEntry> entries, Guid tenantId, SoftTrashFilter filter,
        int offset, int limit, CancellationToken ct = default)
    {
        var existing = await ExistingTablesAsync(ctx, entries, ct);
        var skipped = entries
            .Where(e => !existing.Contains(e.Table) || e.KeyColumn.Length == 0)
            .Select(e => e.Table).Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var listable = entries.Where(e => existing.Contains(e.Table) && e.KeyColumn.Length > 0).ToList();

        var sql = RenderUnionSql(listable, filter);
        if (sql is null) return new SoftTrashPage([], false, skipped);

        var rows = new List<SoftTrashRow>();
        await using (var cmd = ctx.DataSource.CreateCommand(sql))
        {
            cmd.AddParam("w", tenantId);
            if (filter.ProjectId is { } project) cmd.AddParam("p", project);
            cmd.AddParam("lim", limit + 1);
            cmd.AddParam("off", offset);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                rows.Add(new SoftTrashRow(
                    reader.GetFieldValue<string>(0),
                    reader.GetFieldValue<string>(1),
                    reader.GetFieldValue<Guid>(2),
                    await reader.IsDBNullAsync(3, ct) ? null : reader.GetFieldValue<string>(3),
                    await reader.IsDBNullAsync(5, ct) ? null : reader.GetFieldValue<Guid>(5),
                    DateTime.SpecifyKind(reader.GetFieldValue<DateTime>(4), DateTimeKind.Utc),
                    null, null, null, null));
        }

        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        return new SoftTrashPage(await WithSoftDeleteAsync(ctx, rows, ct), hasMore, skipped);
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static string? RenderUnionSql(IReadOnlyList<ISoftEntry> entries, SoftTrashFilter filter)
    {
        var branches = new List<string>();
        foreach (var entry in entries)
            if (Branch(entry, filter) is { } branch) branches.Add(branch);
        if (branches.Count == 0) return null;
        return "SELECT entity, source_table, id, label, deleted_at, project_id FROM ("
            + string.Join(" UNION ALL ", branches)
            + ") u ORDER BY deleted_at DESC, entity, id LIMIT @lim OFFSET @off";
    }

    private static string? Branch(ISoftEntry entry, SoftTrashFilter filter)
    {
        if (entry.KeyColumn.Length == 0) return null;
        if (filter.EntityToken is { } wanted && !string.Equals(entry.EntityToken, wanted, StringComparison.Ordinal)) return null;

        string? tenantPredicate;
        if (entry.Platform)
        {
            if (!filter.IncludePlatform) return null;
            tenantPredicate = null;
        }
        else if (entry.TenantColumn is not { } tenant)
        {
            return null;
        }
        else
        {
            if (string.Equals(tenant, entry.KeyColumn, StringComparison.Ordinal) && !filter.IncludePlatform) return null;
            tenantPredicate = Quote(tenant) + " = @w";
        }

        var projectColumn = ProjectColumnOf(entry);
        if (filter.ProjectId is not null && projectColumn is null) return null;

        var where = new List<string>();
        if (tenantPredicate is not null) where.Add(tenantPredicate);
        where.Add(Quote(entry.SoftColumn) + " IS NOT NULL");
        if (filter.ProjectId is not null) where.Add(Quote(projectColumn!) + " = @p");

        var label = entry.LabelColumn is { } l
            ? "left(" + Quote(l) + "::text, " + LabelMaxChars + ")"
            : "NULL::text";
        var project = projectColumn is { } c ? Quote(c) : "NULL::uuid";

        return "SELECT " + Literal(entry.EntityToken) + " AS entity, " + Literal(entry.Table) + " AS source_table, "
            + Quote(entry.KeyColumn) + " AS id, " + label + " AS label, " + Quote(entry.SoftColumn) + " AS deleted_at, "
            + project + " AS project_id FROM " + Quote(entry.Table) + " WHERE " + string.Join(" AND ", where);
    }

    public static string? ProjectColumnOf(ISoftEntry entry)
    {
        if (entry.Owners.Contains("project_id", StringComparer.Ordinal)) return "project_id";
        return string.Equals(entry.EntityToken, "project", StringComparison.Ordinal) ? entry.KeyColumn : null;
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static async Task<HashSet<string>> ExistingTablesAsync(
        OrmContext ctx, IReadOnlyList<ISoftEntry> entries, CancellationToken ct)
    {
        var known = Present.GetOrCreateValue(ctx.DataSource);
        var wanted = entries.Select(e => e.Table).Distinct(StringComparer.Ordinal).ToList();
        foreach (var table in wanted) Check(table);

        var unknown = wanted.Where(t => !known.ContainsKey(t)).ToArray();
        if (unknown.Length > 0)
        {
            await using var cmd = ctx.DataSource.CreateCommand(ExistingTablesSql);
            cmd.AddParam("tables", unknown);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) known[reader.GetFieldValue<string>(0)] = true;
        }
        return wanted.Where(t => known.ContainsKey(t)).ToHashSet(StringComparer.Ordinal);
    }

    private static async Task<IReadOnlyList<SoftTrashRow>> WithSoftDeleteAsync(
        OrmContext ctx, List<SoftTrashRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return rows;
        var found = new Dictionary<(string Table, Guid Id), (Guid? Actor, string Kind, long Xact, DateTime At)>();
        await using (var cmd = ctx.DataSource.CreateCommand(SoftDeleteBatchSql))
        {
            cmd.AddParam("tables", rows.Select(r => r.Table).ToArray());
            cmd.AddParam("ids", rows.Select(r => r.Id).ToArray());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                found[(reader.GetFieldValue<string>(0), reader.GetFieldValue<Guid>(1))] = (
                    await reader.IsDBNullAsync(2, ct) ? null : reader.GetFieldValue<Guid>(2),
                    reader.GetFieldValue<string>(3),
                    reader.GetFieldValue<long>(4),
                    DateTime.SpecifyKind(reader.GetFieldValue<DateTime>(5), DateTimeKind.Utc));
        }
        return rows
            .Select(r => found.TryGetValue((r.Table, r.Id), out var h)
                ? r with { ActorUserId = h.Actor, ActorKind = h.Kind, XactId = h.Xact, ChangedAt = h.At }
                : r)
            .ToList();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static string Quote(string identifier) => "\"" + Check(identifier) + "\"";

    private static string Literal(string identifier) => "'" + Check(identifier) + "'";

    private static string Check(string identifier) =>
        Identifier.IsMatch(identifier) ? identifier : throw new ArgumentException("not an identifier: " + identifier);
}
