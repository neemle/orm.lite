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
    private const string SoftDeleteOp = HistoryStatements.SoftDeleteOp;

    public static async Task<SoftRestorePlan> PlanAsync(
        UnitOfWork uow, ISoftEntry root, Guid rootId, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(root);

        var (op, xact, changedAt) = await LatestHistoryAsync(uow, root, rootId, tenantId);
        var batched = string.Equals(op, SoftDeleteOp, StringComparison.Ordinal);

        var byTable = new SortedDictionary<string, SortedSet<Guid>>(StringComparer.Ordinal)
        {
            [root.Table] = [rootId],
        };
        if (batched)
            await AddBatchMembersAsync(uow, byTable, root.Platform ? null : tenantId, xact!.Value, changedAt!.Value);

        var rows = await LockTablesAsync(uow, root, byTable, tenantId);

        var rootLocked = rows.Any(
            r => r.Id == rootId && string.Equals(r.Entry.Table, root.Table, StringComparison.Ordinal));
        if (!rootLocked)
            return new SoftRestorePlan([], null, null, await RootStateAsync(uow, root, rootId, tenantId));
        return new SoftRestorePlan(
            rows, batched ? xact : null, batched ? changedAt : null, SoftRestoreRootState.Deleted);
    }

    public static async Task<bool?> IsDeletedUnderShareLockAsync(
        UnitOfWork uow, ISoftEntry entry, Guid key, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(entry);

        var statement = HistoryStatements.Compile(uow.Provider, HistoryStatements.ShareLock(entry, key, tenantId));
        await using var cmd = HistoryStatements.Command(uow.Connection, uow.Transaction, statement);
        return await cmd.RunScalarAsync(statement.Kind) is { } deleted and not DBNull
            ? HistoryReads.Flag(deleted)
            : null;
    }

    public static async Task<int> ApplyAsync(OrmContext ctx, UnitOfWork uow, SoftRestorePlan plan, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(plan);

        var restored = 0;
        var groups = plan.Rows.GroupBy(r => r.Entry.Table, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var entry = group.First().Entry;
            var ids = group.Select(r => r.Id).ToList();
            var affected = await entry.Accept(new RestoreVisitor(ctx, uow, ids, entry.Platform ? null : tenantId));
            if (affected != ids.Count)
                throw new SoftRestoreException(
                    "restore of " + entry.Table + " changed " + affected + " of " + ids.Count
                    + " locked rows; nothing is committed");
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
            == (other.TenantColumn is not null
                && string.Equals(other.TenantColumn, other.KeyColumn, StringComparison.Ordinal));

    private static async Task<(string? Op, long? Xact, DateTime? ChangedAt)> LatestHistoryAsync(
        UnitOfWork uow, ISoftEntry root, Guid rowId, Guid tenantId)
    {
        var statement = HistoryStatements.Compile(uow.Provider, HistoryStatements.LatestVersion(root, rowId, tenantId));
        await using var cmd = HistoryStatements.Command(uow.Connection, uow.Transaction, statement);
        await using var reader = await cmd.RunReaderAsync(statement.Kind);
        if (!await reader.ReadAsync()) return (null, null, null);
        return (reader.GetFieldValue<string>(0), reader.GetFieldValue<long>(1), HistoryReads.Instant(reader, 2));
    }

    private static async Task<List<SoftRestoreRow>> LockTablesAsync(
        UnitOfWork uow, ISoftEntry root, SortedDictionary<string, SortedSet<Guid>> byTable, Guid tenantId)
    {
        var entries = OrmMeta.SoftTypedEntries();
        var rows = new List<SoftRestoreRow>();
        foreach (var (table, ids) in byTable)
        {
            var entry = entries.FirstOrDefault(
                    e => string.Equals(e.Table, table, StringComparison.Ordinal) && e.KeyColumn.Length > 0)
                ?? throw new SoftRestoreException(
                    "no soft-delete registry entry for table " + table + "; the batch cannot be restored whole");
            if (!SameScope(root, entry)) continue;
            rows.AddRange(await LockAsync(uow, entry, ids, tenantId));
        }
        return rows;
    }

    private static async Task AddBatchMembersAsync(
        UnitOfWork uow, SortedDictionary<string, SortedSet<Guid>> byTable, Guid? tenantId, long xact,
        DateTime changedAt)
    {
        foreach (var (table, id) in await BatchMembersAsync(uow, tenantId, xact, changedAt))
        {
            if (!byTable.TryGetValue(table, out var ids)) byTable[table] = ids = [];
            ids.Add(id);
        }
    }

    private static async Task<List<(string Table, Guid Id)>> BatchMembersAsync(
        UnitOfWork uow, Guid? tenantId, long xact, DateTime changedAt)
    {
        var members = new List<(string, Guid)>();
        var statement = HistoryStatements.Compile(
            uow.Provider, HistoryStatements.BatchMembers(tenantId, xact, changedAt));
        await using var cmd = HistoryStatements.Command(uow.Connection, uow.Transaction, statement);
        await using var reader = await cmd.RunReaderAsync(statement.Kind);
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

        var selected = new[] { entry.KeyColumn }.Concat(ownerColumns).ToList();
        var statement = HistoryStatements.Compile(
            uow.Provider, HistoryStatements.LockDeleted(entry, selected, ids, tenantId));
        var rows = new List<SoftRestoreRow>();
        await using var cmd = HistoryStatements.Command(uow.Connection, uow.Transaction, statement);
        await using var reader = await cmd.RunReaderAsync(statement.Kind);
        while (await reader.ReadAsync())
        {
            var values = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < ownerColumns.Count; i++)
                values[ownerColumns[i]] = await reader.IsDBNullAsync(i + 1)
                    ? null
                    : HistoryReads.OwnerValue(
                        reader.GetValue(i + 1), entry.Owners.Contains(ownerColumns[i], StringComparer.Ordinal));
            rows.Add(new SoftRestoreRow(entry, reader.GetFieldValue<Guid>(0), values));
        }
        return rows;
    }

    private sealed class RestoreVisitor(OrmContext ctx, UnitOfWork uow, IReadOnlyCollection<Guid> ids, Guid? tenantId)
        : ISoftEntryVisitor<Task<int>>
    {
        public Task<int> Visit<T>(SoftEntry<T> entry) where T : class =>
            entry.WriteQuery(ctx, uow).RestoreAsync(ids, tenantId);
    }
}
