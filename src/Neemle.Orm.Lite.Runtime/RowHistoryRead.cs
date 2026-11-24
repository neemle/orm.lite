#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Common;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Neemle.Orm.Lite;

public sealed record RowHistoryVersion(
    long Id, string Op, Guid? ActorUserId, string ActorKind, string[] ChangedKeys, long XactId, DateTime ChangedAt);

public static class RowHistoryRead
{
    public static async Task<bool> RowExistsAsync(
        OrmContext ctx, string sourceTable, Guid rowId, Guid? tenantScope = null, CancellationToken ct = default)
    {
        var meta = Resolve(sourceTable);
        var keyColumn = KeyOf(meta, sourceTable);
        var tenantColumn = ScopeOf(nameof(RowExistsAsync), meta, tenantScope);
        var statement = HistoryStatements.Compile(
            ctx.Provider, HistoryStatements.RowExists(sourceTable, keyColumn, tenantColumn, rowId, tenantScope));
        await using var cmd = ctx.DataSource.CreateCommand(statement.Text);
        HistoryStatements.Bind(cmd, statement);
        return HistoryReads.Flag(await cmd.ExecuteScalarAsync(ct));
    }

    public static async Task<IReadOnlyList<RowHistoryVersion>> VersionsAsync(
        OrmContext ctx, string sourceTable, Guid rowId, Guid? tenantScope = null, CancellationToken ct = default)
    {
        var meta = Resolve(sourceTable);
        var tenantColumn = ScopeOf(nameof(VersionsAsync), meta, tenantScope);
        var keyColumn = tenantColumn is null ? null : KeyOf(meta, sourceTable);
        var statement = HistoryStatements.Compile(
            ctx.Provider,
            HistoryStatements.Versions(sourceTable, keyColumn, tenantColumn, sourceTable, rowId, tenantScope));
        await using var cmd = ctx.DataSource.CreateCommand(statement.Text);
        HistoryStatements.Bind(cmd, statement);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var versions = new List<RowHistoryVersion>();
        while (await reader.ReadAsync(ct))
            versions.Add(new RowHistoryVersion(
                reader.GetFieldValue<long>(0),
                reader.GetFieldValue<string>(1),
                await reader.IsDBNullAsync(2, ct) ? null : reader.GetFieldValue<Guid>(2),
                reader.GetFieldValue<string>(3),
                HistoryReads.Keys(reader, 4),
                reader.GetFieldValue<long>(5),
                HistoryReads.Instant(reader, 6)));
        return versions;
    }

    public static async Task<JsonDocument?> StateOfVersionAsync(
        OrmContext ctx, string sourceTable, Guid rowId, long versionId, Guid? tenantScope = null,
        CancellationToken ct = default)
    {
        var meta = Resolve(sourceTable);
        var keyColumn = KeyOf(meta, sourceTable);
        var tenantColumn = ScopeOf(nameof(StateOfVersionAsync), meta, tenantScope);
        var tree = HistoryStatements.State(
            sourceTable, keyColumn, meta.Unversioned ?? [], tenantColumn, OrmMeta.ColumnsForTable(sourceTable) ?? [],
            sourceTable, rowId, versionId, tenantScope);
        var statement = HistoryStatements.Compile(ctx.Provider, tree);
        await using var cmd = ctx.DataSource.CreateCommand(statement.Text);
        HistoryStatements.Bind(cmd, statement);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return JsonDocument.Parse(reader.GetFieldValue<string>(0));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string RenderStateSql(
        ISqlProvider provider, string table, string keyColumn, IReadOnlyList<string> unversioned,
        string? tenantColumn = null) =>
        HistoryStatements.Compile(
            provider,
            HistoryStatements.State(
                table, keyColumn, unversioned, tenantColumn, OrmMeta.ColumnsForTable(table) ?? [],
                table, Guid.Empty, 0, null)).Text;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string RenderVersionsSql(
        ISqlProvider provider, string table, string? keyColumn, string? tenantColumn) =>
        HistoryStatements.Compile(
            provider, HistoryStatements.Versions(table, keyColumn, tenantColumn, table, Guid.Empty, null)).Text;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string RenderExistsSql(ISqlProvider provider, string table, string keyColumn, string? tenantColumn) =>
        HistoryStatements.Compile(
            provider, HistoryStatements.RowExists(table, keyColumn, tenantColumn, Guid.Empty, null)).Text;

    private static string KeyOf(SoftDeleteMeta meta, string sourceTable) =>
        meta.KeyColumn ?? throw new InvalidOperationException("no key column registered for " + sourceTable);

    private static string? ScopeOf(string verb, SoftDeleteMeta meta, Guid? tenantScope) =>
        HistoryScope.Column(verb, meta.Platform, meta.TenantColumn, tenantScope is not null);

    private static SoftDeleteMeta Resolve(string sourceTable)
    {
        foreach (var entry in OrmMeta.SoftEntries())
            if (string.Equals(entry.Table, sourceTable, StringComparison.Ordinal))
                return entry.Meta;
        throw new ArgumentException("not a registered soft table: " + sourceTable, nameof(sourceTable));
    }
}
