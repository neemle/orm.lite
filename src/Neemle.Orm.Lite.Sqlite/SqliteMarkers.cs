using System.Globalization;
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite.Sqlite;

internal sealed partial class SqliteMigrationUnit
{
    private static string MarkerKey(string kind, string name) => SqliteCatalog.MarkerKey(kind, name);

    private static Node EnsureMarkerTable() =>
        new CreateTable(SqliteDataLiterals.MarkerTable, [
            new ColumnDef(SqliteDataLiterals.MarkerKeyColumn, new TypeRef(ColumnType.Text), false, true),
            new ColumnDef(SqliteDataLiterals.MarkerValueColumn, new TypeRef(ColumnType.Text), false),
        ]);

    private static Node ForgetMarker(string key) =>
        new DeleteData(SqliteDataLiterals.MarkerTable, SqliteDataLiterals.MarkerKeyColumn, [new StringLit(key)]);

    private static IEnumerable<Node> RememberMarker(string key, string value) =>
    [
        EnsureMarkerTable(),
        ForgetMarker(key),
        new InsertData(
            SqliteDataLiterals.MarkerTable,
            [new ColumnRef(SqliteDataLiterals.MarkerKeyColumn), new ColumnRef(SqliteDataLiterals.MarkerValueColumn)],
            [new DataRow([new StringLit(key), new StringLit(value)])]),
    ];

    private static Task<string?> ReadMarkerAsync(SqliteCatalog catalog, string key, CancellationToken ct) => catalog.MarkerAsync(key, ct);

    private async Task<List<Statement>> MarkerRemovalAsync(SqliteCatalog catalog, string key, CancellationToken ct) =>
        await ReadMarkerAsync(catalog, key, ct) is null ? [] : [Compile(ForgetMarker(key))];

    private async Task<IReadOnlyList<Statement>> SoftDeleteColumnAsync(SqliteCatalog catalog, SoftDeleteColumn node, CancellationToken ct)
    {
        var column = SoftDeleteColumnDdl.Column;
        var key = MarkerKey(SqliteDataLiterals.ColumnKind, node.Table + "." + column);
        var present = (await catalog.TableColumnsAsync(node.Table, ct)).Any(c => Same(c.Name, column));
        if (!node.Remove)
        {
            if (present) return [];
            var add = new AddColumn(node.Table, new ColumnDef(column, new TypeRef(ColumnType.Timestamp)), false);
            return Compile([add, .. RememberMarker(key, node.Version)]);
        }
        if (await ReadMarkerAsync(catalog, key, ct) != node.Version) return [];
        var count = new Select(
            [new Call(Fn.Count, [new Star()])],
            new TableRef(node.Table),
            new Where([new IsNotNull(new ColumnRef(column))]));
        var soft = Convert.ToInt64((await catalog.RowsAsync(count, ct))[0][0], CultureInfo.InvariantCulture);
        if (soft > 0)
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, SqliteDataLiterals.SoftRowsMessage, column, node.Table, soft));
        var statements = (await DropColumnAsync(catalog, new DropColumn(node.Table, column, false), ct)).ToList();
        statements.AddRange(await MarkerRemovalAsync(catalog, key, ct));
        return statements;
    }

    private static string ColumnOf(Node term) => term switch
    {
        ColumnRef c => c.Column,
        Call { Function: Fn.Lower, Arguments: [ColumnRef c] } => c.Column,
        _ => throw new ArgumentException($"a key term is a column or a lowered column, not {term.Name}.", nameof(term)),
    };

    private async Task<IReadOnlyList<Statement>> LiveOnlyIndexAsync(SqliteCatalog catalog, ConvertToLiveOnlyIndex node, CancellationToken ct)
    {
        var key = MarkerKey(SqliteDataLiterals.IndexKind, node.LiveIndex);
        if (!node.Revert)
        {
            var live = new CreateIndex(node.LiveIndex, node.Table, node.KeyTerms, true, new Where([new IsNull(new ColumnRef(node.SoftColumn))]));
            var origin = (await catalog.RowsAsync(new SqlitePragma("index_list", node.Table), ct))
                .Where(r => Same((string)r[1]!, node.TotalIndex)).Select(r => (string)r[3]!).FirstOrDefault();
            if (origin is null) return [Compile(live)];
            if (origin != SqliteDataLiterals.ExplicitIndexOrigin)
                throw new NotSupportedException(
                    $"index '{node.TotalIndex}' is owned by a constraint of table '{node.Table}'; SQLite cannot drop a constraint's index, so only a plain unique index converts.");
            return Compile([live, .. RememberMarker(key, node.Version), new DropIndex(node.TotalIndex, false)]);
        }
        if (await ReadMarkerAsync(catalog, key, ct) != node.Version) return [];
        var columns = node.KeyTerms.Select(ColumnOf).Distinct(StringComparer.Ordinal).Select(c => (Node)new ColumnRef(c)).ToList();
        if ((await catalog.RowsAsync(new SqliteDuplicateKeys(node.Table, node.KeyTerms, NodeList.Of(columns)), ct)).Count > 0)
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture, SqliteDataLiterals.DuplicateKeyMessage, node.TotalIndex, node.Table,
                string.Join(SqliteDataLiterals.ListSeparator, node.KeyTerms.Select(ColumnOf))));
        var restore = new List<Node> { new CreateIndex(node.TotalIndex, node.Table, node.KeyTerms, true) };
        if (!node.LiveIsDeclared) restore.Add(new DropIndex(node.LiveIndex, false));
        restore.Add(ForgetMarker(key));
        return Compile(restore);
    }
}
