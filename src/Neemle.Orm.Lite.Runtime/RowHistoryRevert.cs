#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace Neemle.Orm.Lite;

public sealed record RevertRowLock(bool Deleted, Guid? ProjectId);

public static class RowHistoryRevert
{
    internal const string UpdateStamp = "updated_at";

    public static async Task<RevertRowLock?> LockAsync(UnitOfWork uow, ISoftEntry entry, Guid rowId, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(entry);

        var statement = HistoryStatements.Compile(uow.Provider, HistoryStatements.LockRow(entry, rowId, tenantId));
        await using var cmd = HistoryStatements.Command(uow.Connection, uow.Transaction, statement);
        await using var reader = await cmd.RunReaderAsync(statement.Kind);
        if (!await reader.ReadAsync()) return null;
        var project = await reader.IsDBNullAsync(1) ? null : (Guid?)reader.GetFieldValue<Guid>(1);
        return new RevertRowLock(HistoryReads.Flag(reader.GetValue(0)), project);
    }

    public static async Task<bool> VersionExistsAsync(
        UnitOfWork uow, ISoftEntry entry, Guid rowId, long versionId, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(entry);

        var statement = HistoryStatements.Compile(
            uow.Provider, HistoryStatements.VersionExists(entry, rowId, versionId, tenantId));
        await using var cmd = HistoryStatements.Command(uow.Connection, uow.Transaction, statement);
        return HistoryReads.Flag(await cmd.RunScalarAsync(statement.Kind));
    }

    public static async Task<int> RevertAsync(
        UnitOfWork uow, ISoftEntry entry, Guid rowId, long versionId, Guid tenantId, bool includeStatus)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(entry);

        var statement = HistoryStatements.Compile(
            uow.Provider,
            HistoryStatements.Revert(entry, includeStatus, entry.Table, rowId, versionId, tenantId),
            StatementKind.Write);
        await using var cmd = HistoryStatements.Command(uow.Connection, uow.Transaction, statement);
        return await cmd.RunNonQueryAsync(statement.Kind);
    }

    public static async Task<long> NewestVersionAsync(UnitOfWork uow, ISoftEntry entry, Guid rowId, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(uow);
        ArgumentNullException.ThrowIfNull(entry);

        var statement = HistoryStatements.Compile(
            uow.Provider, HistoryStatements.NewestVersion(entry, rowId, tenantId));
        await using var cmd = HistoryStatements.Command(uow.Connection, uow.Transaction, statement);
        return await cmd.RunScalarAsync(statement.Kind) is long newest
            ? newest
            : throw new InvalidOperationException("no history row for " + entry.Table + " " + rowId);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IReadOnlyList<string> RevertedColumns(ISoftEntry entry, bool includeStatus)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var fixedColumns = entry.Pinned.Concat(entry.Unversioned);
        var excluded = new HashSet<string>(fixedColumns, StringComparer.Ordinal) { UpdateStamp };
        if (!includeStatus) excluded.UnionWith(entry.StatusColumns);
        return [.. OrmMeta.Get(entry.ClrType).Columns.Where(c => !excluded.Contains(c))];
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string RenderSql(ISqlProvider provider, ISoftEntry entry, bool includeStatus)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return HistoryStatements.Compile(
            provider,
            HistoryStatements.Revert(entry, includeStatus, entry.Table, Guid.Empty, 0, null),
            StatementKind.Write).Text;
    }
}
