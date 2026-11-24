using System.Globalization;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Sqlite;

internal sealed partial class SqliteMigrationUnit
{
    private static IReadOnlyList<Node> HistoryIndexes() =>
    [
        new CreateIndex(
            SqliteHistoryDataLiterals.SourceRowIndex, SqliteHistoryDataLiterals.Table,
            [new ColumnRef("source_table"), new ColumnRef("row_id"), new ColumnRef("id")]),
        new CreateIndex(
            SqliteHistoryDataLiterals.TenantXactIndex, SqliteHistoryDataLiterals.Table,
            [new ColumnRef("tenant_id"), new ColumnRef("xact_id")]),
        new CreateIndex(
            SqliteHistoryDataLiterals.ChangedAtIndex, SqliteHistoryDataLiterals.Table,
            [new ColumnRef("changed_at")]),
    ];

    private static string HistoryKey(string table) => MarkerKey(SqliteHistoryDataLiterals.MarkerKind, table);

    private async Task<IReadOnlyList<Statement>> HistoryInstallAsync(SqliteCatalog catalog, RowHistoryInstall node, CancellationToken ct)
    {
        var statements = Compile([new SqliteHistoryStore(), .. HistoryIndexes()]);
        foreach (var table in node.Tables.Cast<HistoryTableNode>())
            statements.AddRange(await AttachHistoryAsync(catalog, table, ct));
        return statements;
    }

    private async Task<IReadOnlyList<Statement>> AttachHistoryAsync(SqliteCatalog catalog, HistoryTableNode node, CancellationToken ct)
    {
        if (!await catalog.TableExistsAsync(node.Table, ct)) return [];
        var spec = SqliteHistorySpec.Of(node);
        var columns = (await catalog.TableColumnsAsync(node.Table, ct)).Select(c => c.Name).ToList();
        spec.Bind(node.Table, columns);
        var wanted = Compile(spec.Triggers(node.Table, columns));
        var (owned, _) = await OwnedHistoryAsync(catalog, await catalog.ObjectsAsync(ct), node.Table, ct);
        var present = owned.Select(t => t.Sql).Order(StringComparer.Ordinal).ToList();
        var current = await ReadMarkerAsync(catalog, HistoryKey(node.Table), ct);
        if (current == spec.Encode()
            && present.SequenceEqual(wanted.Select(s => s.Text).Order(StringComparer.Ordinal), StringComparer.Ordinal))
            return [];
        var statements = Compile(SqliteHistorySpec.Drops(node.Table));
        statements.AddRange(wanted);
        statements.AddRange(Compile(RememberMarker(HistoryKey(node.Table), spec.Encode())));
        return statements;
    }

    private async Task<(IReadOnlyList<SchemaObject> Owned, string? Marker)> OwnedHistoryAsync(
        SqliteCatalog catalog, IEnumerable<SchemaObject> objects, string table, CancellationToken ct)
    {
        var named = objects
            .Where(o => o.Type == SqliteDataLiterals.TriggerType && Same(o.Table, table))
            .Where(o => provider.Triggers.Owns(o.Name))
            .ToList();
        if (named.Count == 0) return ([], null);
        var marker = await ReadMarkerAsync(catalog, HistoryKey(table), ct);
        return (named.Where(o => provider.Triggers.Owns(table, o.Name, marker)).ToList(), marker);
    }

    private async Task<IReadOnlyList<Statement>> WithHistoryAsync(
        SqliteCatalog catalog, string table, IReadOnlyList<Statement> change, Func<IReadOnlyList<string>, IReadOnlyList<string>> columnsAfter,
        Func<SqliteHistorySpec, SqliteHistorySpec>? respec, CancellationToken ct)
    {
        var (found, marker) = await OwnedHistoryAsync(catalog, await catalog.ObjectsAsync(ct), table, ct);
        if (found.Count == 0) return change;
        var owned = found.Select(o => o.Name).ToList();
        var current = SqliteHistorySpec.Decode(marker)
            ?? throw new NotSupportedException(string.Format(
                CultureInfo.InvariantCulture,
                SqliteHistoryDataLiterals.RefusedUnmarked,
                table,
                string.Join(SqliteDataLiterals.ListSeparator, owned)));
        var columns = columnsAfter((await catalog.TableColumnsAsync(table, ct)).Select(c => c.Name).ToList());
        var spec = respec is null ? current : respec(current);
        spec.Bind(table, columns);
        var statements = Compile(SqliteHistorySpec.Drops(table));
        statements.AddRange(change);
        statements.AddRange(Compile(spec.Triggers(table, columns)));
        if (spec.Encode() != current.Encode()) statements.AddRange(Compile(RememberMarker(HistoryKey(table), spec.Encode())));
        return statements;
    }

    private async Task<IReadOnlyList<Statement>> RenameColumnAsync(SqliteCatalog catalog, RenameColumn node, CancellationToken ct) =>
        await WithHistoryAsync(
            catalog,
            node.Table,
            [Compile(node)],
            columns => columns.Select(c => Same(c, node.From) ? node.To : c).ToList(),
            spec => spec with
            {
                KeyColumn = Same(spec.KeyColumn, node.From) ? node.To : spec.KeyColumn,
                SoftColumn = Same(spec.SoftColumn, node.From) ? node.To : spec.SoftColumn,
                TenantColumn = spec.TenantColumn is { } tenant && Same(tenant, node.From) ? node.To : spec.TenantColumn,
                Unversioned = spec.Unversioned.Select(c => Same(c, node.From) ? node.To : c).ToList(),
            },
            ct);
}
