#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace Neemle.Orm.Lite;

public sealed record RevertRowLock(bool Deleted, Guid? ProjectId);

public static class RowHistoryRevert
{
    private const string UpdateStamp = "updated_at";

    private const string NewestVersionSql = "SELECT max(id) FROM row_history WHERE source_table = @t AND row_id = @id";

    private const string VersionExistsSql =
        "SELECT EXISTS (SELECT 1 FROM row_history WHERE id = @k AND source_table = @t AND row_id = @id)";

    public static async Task<RevertRowLock?> LockAsync(UnitOfWork uow, ISoftEntry entry, Guid rowId, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(entry);

        var tenant = ScopeColumn(entry);
        var project = SoftTrashRead.ProjectColumnOf(entry);
        var sql = "SELECT " + SoftTrashRead.Quote(entry.SoftColumn) + " IS NOT NULL, "
            + (project is null ? "NULL::uuid" : SoftTrashRead.Quote(project))
            + " FROM " + SoftTrashRead.Quote(entry.Table)
            + " WHERE " + SoftTrashRead.Quote(entry.KeyColumn) + " = @id"
            + (tenant is null ? "" : " AND " + SoftTrashRead.Quote(tenant) + " = @w")
            + " FOR UPDATE";
        await using var cmd = Command(uow, sql);
        cmd.AddParam("id", rowId);
        if (tenant is not null) cmd.AddParam("w", tenantId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new RevertRowLock(reader.GetBoolean(0), await reader.IsDBNullAsync(1) ? null : reader.GetFieldValue<Guid>(1));
    }

    public static async Task<bool> VersionExistsAsync(UnitOfWork uow, ISoftEntry entry, Guid rowId, long versionId)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(entry);

        await using var cmd = Command(uow, VersionExistsSql);
        cmd.AddParam("t", entry.Table);
        cmd.AddParam("id", rowId);
        cmd.AddParam("k", versionId);
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    public static async Task<int> RevertAsync(
        UnitOfWork uow, ISoftEntry entry, Guid rowId, long versionId, Guid tenantId, bool includeStatus)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(entry);

        var tenant = ScopeColumn(entry);
        await using var cmd = Command(uow, RenderSql(entry, includeStatus));
        cmd.AddParam("t", entry.Table);
        cmd.AddParam("id", rowId);
        cmd.AddParam("k", versionId);
        if (tenant is not null) cmd.AddParam("w", tenantId);
        return await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<long> NewestVersionAsync(UnitOfWork uow, ISoftEntry entry, Guid rowId)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(entry);

        await using var cmd = Command(uow, NewestVersionSql);
        cmd.AddParam("t", entry.Table);
        cmd.AddParam("id", rowId);
        return await cmd.ExecuteScalarAsync() is long newest
            ? newest
            : throw new InvalidOperationException("no history row for " + entry.Table + " " + rowId);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IReadOnlyList<string> RevertedColumns(ISoftEntry entry, bool includeStatus)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var excluded = new HashSet<string>(entry.Pinned.Concat(entry.Unversioned), StringComparer.Ordinal) { UpdateStamp };
        if (!includeStatus) excluded.UnionWith(entry.StatusColumns);
        return [.. OrmMeta.Get(entry.ClrType).Columns.Where(c => !excluded.Contains(c))];
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string RenderSql(ISoftEntry entry, bool includeStatus)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var tenant = ScopeColumn(entry);
        var table = SoftTrashRead.Quote(entry.Table);
        var assignments = RevertedColumns(entry, includeStatus)
            .Select(c => SoftTrashRead.Quote(c) + " = r." + SoftTrashRead.Quote(c))
            .ToList();
        if (OrmMeta.Get(entry.ClrType).Columns.Contains(UpdateStamp, StringComparer.Ordinal))
            assignments.Add(SoftTrashRead.Quote(UpdateStamp) + " = now()");
        if (assignments.Count == 0)
            throw new InvalidOperationException("nothing to revert on " + entry.Table);

        var state = RowHistoryRead.RenderStateSql(entry.Table, entry.KeyColumn, entry.Unversioned, tenant);
        return "WITH st(state) AS (" + state + ") UPDATE " + table + " AS t SET " + string.Join(", ", assignments)
            + " FROM st CROSS JOIN LATERAL jsonb_populate_record(NULL::" + table + ", st.state) AS r"
            + " WHERE t." + SoftTrashRead.Quote(entry.KeyColumn) + " = @id"
            + (tenant is null ? "" : " AND t." + SoftTrashRead.Quote(tenant) + " = @w")
            + " AND t." + SoftTrashRead.Quote(entry.SoftColumn) + " IS NULL";
    }

    private static string? ScopeColumn(ISoftEntry entry) =>
        RowHistoryRead.ScopeColumn(entry.Platform, entry.TenantColumn, entry.KeyColumn);

    private static DbCommand Command(UnitOfWork uow, string sql)
    {
        var cmd = uow.Connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = uow.Transaction;
        return cmd;
    }
}
