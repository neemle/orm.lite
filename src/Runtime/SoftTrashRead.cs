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

namespace Neemle.OrmLite;

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

    public static async Task<SoftTrashPage> ListAsync(
        OrmContext ctx, IReadOnlyList<ISoftEntry> entries, Guid tenantId, SoftTrashFilter filter,
        int offset, int limit, CancellationToken ct = default)
    {
        var existing = await ExistingTablesAsync(ctx, entries, ct);
        var skipped = entries
            .Where(e => !existing.Contains(e.Table) || e.KeyColumn.Length == 0)
            .Select(e => e.Table).Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToList();
        var listable = entries.Where(e => existing.Contains(e.Table) && e.KeyColumn.Length > 0).ToList();

        var tree = HistoryStatements.TrashUnion(listable, filter, tenantId, limit + 1, offset);
        if (tree is null) return new SoftTrashPage([], false, skipped);

        var statement = HistoryStatements.Compile(ctx.Provider, tree);
        var rows = new List<SoftTrashRow>();
        await using (var cmd = ctx.DataSource.CreateCommand(statement.Text))
        {
            HistoryStatements.Bind(cmd, statement);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                rows.Add(new SoftTrashRow(
                    reader.GetFieldValue<string>(0),
                    reader.GetFieldValue<string>(1),
                    reader.GetFieldValue<Guid>(2),
                    await reader.IsDBNullAsync(3, ct) ? null : reader.GetFieldValue<string>(3),
                    await reader.IsDBNullAsync(5, ct) ? null : reader.GetFieldValue<Guid>(5),
                    HistoryReads.Instant(reader, 4),
                    null, null, null, null));
        }

        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        return new SoftTrashPage(await WithSoftDeleteAsync(ctx, rows, ct), hasMore, skipped);
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static string? RenderUnionSql(ISqlProvider provider, IReadOnlyList<ISoftEntry> entries, SoftTrashFilter filter)
    {
        var tree = HistoryStatements.TrashUnion(entries, filter, Guid.Empty, 0, 0);
        return tree is null ? null : HistoryStatements.Compile(provider, tree).Text;
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
            var statement = HistoryStatements.Compile(ctx.Provider, HistoryStatements.ExistingTables(unknown));
            await using var cmd = ctx.DataSource.CreateCommand(statement.Text);
            HistoryStatements.Bind(cmd, statement);
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
        var statement = HistoryStatements.Compile(
            ctx.Provider, HistoryStatements.SoftDeletes(rows.Select(r => r.Table).ToArray(), rows.Select(r => r.Id).ToArray()));
        await using (var cmd = ctx.DataSource.CreateCommand(statement.Text))
        {
            HistoryStatements.Bind(cmd, statement);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                found[(reader.GetFieldValue<string>(0), reader.GetFieldValue<Guid>(1))] = (
                    await reader.IsDBNullAsync(2, ct) ? null : reader.GetFieldValue<Guid>(2),
                    reader.GetFieldValue<string>(3),
                    reader.GetFieldValue<long>(4),
                    HistoryReads.Instant(reader, 5));
        }
        return rows
            .Select(r => found.TryGetValue((r.Table, r.Id), out var h)
                ? r with { ActorUserId = h.Actor, ActorKind = h.Kind, XactId = h.Xact, ChangedAt = h.At }
                : r)
            .ToList();
    }

    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    public static string Quote(string identifier) => "\"" + Check(identifier) + "\"";

    private static string Check(string identifier) =>
        Identifier.IsMatch(identifier) ? identifier : throw new ArgumentException("not an identifier: " + identifier);
}
