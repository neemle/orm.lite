#nullable enable
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace Neemle.Orm.Lite;

public sealed record SoftRestoreRow(ISoftEntry Entry, Guid Id, IReadOnlyDictionary<string, object?> OwnerValues);

public enum SoftRestoreRootState { Missing, Live, Deleted }

public sealed record SoftRestorePlan(
    IReadOnlyList<SoftRestoreRow> Rows, long? XactId, DateTime? ChangedAt, SoftRestoreRootState RootState);

public sealed class SoftRestoreException(string message) : InvalidOperationException(message);

public static class SoftRestore
{
    private const string LatestHistorySql =
        "SELECT op, xact_id, changed_at FROM row_history WHERE source_table = @t AND row_id = @id ORDER BY id DESC LIMIT 1";

    private const string BatchSql =
        "SELECT h.source_table, h.row_id FROM row_history h"
        + " WHERE h.tenant_id IS NOT DISTINCT FROM @w::uuid AND h.xact_id = @x AND h.changed_at = @c AND h.op = 'soft_delete'"
        + " AND NOT EXISTS (SELECT 1 FROM row_history h2 WHERE h2.source_table = h.source_table AND h2.row_id = h.row_id AND h2.id > h.id)";

    private const string SoftDeleteOp = "soft_delete";

    public static async Task<SoftRestorePlan> PlanAsync(
        UnitOfWork uow, ISoftEntry root, Guid rootId, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(root);

        var (op, xact, changedAt) = await LatestHistoryAsync(uow, root.Table, rootId);
        var batched = string.Equals(op, SoftDeleteOp, StringComparison.Ordinal);

        var byTable = new SortedDictionary<string, SortedSet<Guid>>(StringComparer.Ordinal)
        {
            [root.Table] = [rootId],
        };
        if (batched)
            foreach (var (table, id) in await BatchMembersAsync(uow, root.Platform ? null : tenantId, xact!.Value, changedAt!.Value))
            {
                if (!byTable.TryGetValue(table, out var ids)) byTable[table] = ids = [];
                ids.Add(id);
            }

        var entries = OrmMeta.SoftTypedEntries();
        var rows = new List<SoftRestoreRow>();
        foreach (var (table, ids) in byTable)
        {
            var entry = entries.FirstOrDefault(e => string.Equals(e.Table, table, StringComparison.Ordinal) && e.KeyColumn.Length > 0)
                ?? throw new SoftRestoreException("no soft-delete registry entry for table " + table + "; the batch cannot be restored whole");
            if (!SameScope(root, entry)) continue;
            rows.AddRange(await LockAsync(uow, entry, ids, tenantId));
        }

        var rootLocked = rows.Any(r => r.Id == rootId && string.Equals(r.Entry.Table, root.Table, StringComparison.Ordinal));
        if (!rootLocked)
            return new SoftRestorePlan([], null, null, await RootStateAsync(uow, root, rootId, tenantId));
        return new SoftRestorePlan(rows, batched ? xact : null, batched ? changedAt : null, SoftRestoreRootState.Deleted);
    }

    public static async Task<bool?> IsDeletedUnderShareLockAsync(
        UnitOfWork uow, ISoftEntry entry, Guid key, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(entry);

        var sql = "SELECT " + SoftTrashRead.Quote(entry.SoftColumn) + " IS NOT NULL FROM " + SoftTrashRead.Quote(entry.Table)
            + " WHERE " + SoftTrashRead.Quote(entry.KeyColumn) + " = @k"
            + (entry.Platform ? "" : " AND " + SoftTrashRead.Quote(entry.TenantColumn!) + " = @w")
            + " FOR SHARE";
        await using var cmd = Command(uow, sql);
        cmd.AddParam("k", key);
        if (!entry.Platform) cmd.AddParam("w", tenantId);
        return await cmd.ExecuteScalarAsync() is bool deleted ? deleted : null;
    }

    public static async Task<int> ApplyAsync(OrmContext ctx, UnitOfWork uow, SoftRestorePlan plan, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(plan);

        var restored = 0;
        foreach (var group in plan.Rows.GroupBy(r => r.Entry.Table, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var entry = group.First().Entry;
            var ids = group.Select(r => r.Id).ToList();
            var affected = await entry.Accept(new RestoreVisitor(ctx, uow, ids, entry.Platform ? null : tenantId));
            if (affected != ids.Count)
                throw new SoftRestoreException(
                    "restore of " + entry.Table + " changed " + affected + " of " + ids.Count + " locked rows; nothing is committed");
            restored += affected;
        }
        return restored;
    }

    private static async Task<SoftRestoreRootState> RootStateAsync(
        UnitOfWork uow, ISoftEntry root, Guid rootId, Guid tenantId) =>
        await IsDeletedUnderShareLockAsync(uow, root, rootId, tenantId) switch
        {
            null => SoftRestoreRootState.Missing,
            true => SoftRestoreRootState.Deleted,
            false => SoftRestoreRootState.Live,
        };

    private static bool SameScope(ISoftEntry root, ISoftEntry other) =>
        root.Platform == other.Platform
        && (root.TenantColumn is not null && string.Equals(root.TenantColumn, root.KeyColumn, StringComparison.Ordinal))
            == (other.TenantColumn is not null && string.Equals(other.TenantColumn, other.KeyColumn, StringComparison.Ordinal));

    private static async Task<(string? Op, long? Xact, DateTime? ChangedAt)> LatestHistoryAsync(
        UnitOfWork uow, string table, Guid rowId)
    {
        await using var cmd = Command(uow, LatestHistorySql);
        cmd.AddParam("t", table);
        cmd.AddParam("id", rowId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return (null, null, null);
        return (
            reader.GetFieldValue<string>(0),
            reader.GetFieldValue<long>(1),
            DateTime.SpecifyKind(reader.GetFieldValue<DateTime>(2), DateTimeKind.Utc));
    }

    private static async Task<List<(string Table, Guid Id)>> BatchMembersAsync(
        UnitOfWork uow, Guid? tenantId, long xact, DateTime changedAt)
    {
        var members = new List<(string, Guid)>();
        await using var cmd = Command(uow, BatchSql);
        cmd.AddParam("w", tenantId);
        cmd.AddParam("x", xact);
        cmd.AddParam("c", changedAt);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            members.Add((reader.GetFieldValue<string>(0), reader.GetFieldValue<Guid>(1)));
        return members;
    }

    private static async Task<List<SoftRestoreRow>> LockAsync(
        UnitOfWork uow, ISoftEntry entry, IReadOnlyCollection<Guid> ids, Guid tenantId)
    {
        var ownerColumns = new List<string>();
        foreach (var owner in entry.Owners)
        {
            if (!ownerColumns.Contains(owner, StringComparer.Ordinal)) ownerColumns.Add(owner);
            if (entry.TypeColumnOfOwner(owner) is { } typed && !ownerColumns.Contains(typed, StringComparer.Ordinal))
                ownerColumns.Add(typed);
        }

        var selected = string.Join(", ", new[] { entry.KeyColumn }.Concat(ownerColumns).Select(SoftTrashRead.Quote));
        var sql = "SELECT " + selected + " FROM " + SoftTrashRead.Quote(entry.Table)
            + " WHERE " + SoftTrashRead.Quote(entry.KeyColumn) + " = ANY(@ids)"
            + (entry.Platform ? "" : " AND " + SoftTrashRead.Quote(entry.TenantColumn!) + " = @w")
            + " AND " + SoftTrashRead.Quote(entry.SoftColumn) + " IS NOT NULL"
            + " ORDER BY " + SoftTrashRead.Quote(entry.KeyColumn) + " FOR UPDATE";

        var rows = new List<SoftRestoreRow>();
        await using var cmd = Command(uow, sql);
        cmd.AddParam("ids", ids.ToArray());
        if (!entry.Platform) cmd.AddParam("w", tenantId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < ownerColumns.Count; i++)
                values[ownerColumns[i]] = await reader.IsDBNullAsync(i + 1) ? null : reader.GetValue(i + 1);
            rows.Add(new SoftRestoreRow(entry, reader.GetFieldValue<Guid>(0), values));
        }
        return rows;
    }

    private static DbCommand Command(UnitOfWork uow, string sql)
    {
        var cmd = uow.Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = uow.Transaction;
        return cmd;
    }

    private sealed class RestoreVisitor(OrmContext ctx, UnitOfWork uow, IReadOnlyCollection<Guid> ids, Guid? tenantId)
        : ISoftEntryVisitor<Task<int>>
    {
        public Task<int> Visit<T>(SoftEntry<T> entry) where T : class =>
            entry.WriteQuery(ctx, uow).RestoreAsync(ids, tenantId);
    }
}
