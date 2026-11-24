using System.Data.Common;
using System.Globalization;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Sqlite;

internal sealed record SchemaObject(string Type, string Name, string Table, string Sql);

internal sealed record TableColumn(string Name, string Type, bool NotNull, int KeyPosition, string? Default, int Hidden)
{
    internal bool Copyable => Hidden == 0;
}

internal sealed class SqliteCatalog(SqliteProvider provider, DbConnection connection, DbTransaction? transaction) : ISchemaCatalog
{
    private static long Int(object? value) => Convert.ToInt64(value, CultureInfo.InvariantCulture);

    internal async Task<List<object?[]>> RowsAsync(Node query, CancellationToken ct)
    {
        var statement = StatementCompiler.Compile(query, provider, StatementKind.Read);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = statement.Text;
        foreach (var parameter in statement.Parameters) command.AddParam(parameter.Name, parameter.Value);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(ct))
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    internal async Task<List<SchemaObject>> ObjectsAsync(CancellationToken ct)
    {
        var query = new Select(
            [new ColumnRef("type"), new ColumnRef("name"), new ColumnRef("tbl_name"), new ColumnRef("sql")],
            new TableRef(SqliteDataLiterals.SchemaTable),
            new Where([new IsNotNull(new ColumnRef("sql"))]),
            new OrderBy([new OrderTerm(new ColumnRef("rowid"), false)]));
        return (await RowsAsync(query, ct))
            .Select(r => new SchemaObject((string)r[0]!, (string)r[1]!, (string)r[2]!, (string)r[3]!))
            .ToList();
    }

    internal static SchemaObject? TableOf(IEnumerable<SchemaObject> objects, string table) =>
        objects.FirstOrDefault(o => o.Type == SqliteDataLiterals.TableType && string.Equals(o.Name, table, StringComparison.OrdinalIgnoreCase));

    internal async Task<List<TableColumn>> TableColumnsAsync(string table, CancellationToken ct) =>
        (await RowsAsync(new SqlitePragma("table_xinfo", table), ct))
            .Select(r => new TableColumn((string)r[1]!, (string?)r[2] ?? "", Int(r[3]) != 0, (int)Int(r[5]), (string?)r[4], (int)Int(r[6])))
            .Where(c => c.Hidden != 1)
            .ToList();

    internal static string MarkerKey(string kind, string name) => kind + SqliteDataLiterals.MarkerSeparator + name;

    internal async Task<string?> MarkerAsync(string key, CancellationToken ct)
    {
        if (!await TableExistsAsync(SqliteDataLiterals.MarkerTable, ct)) return null;
        var query = new Select(
            [new ColumnRef(SqliteDataLiterals.MarkerValueColumn)],
            new TableRef(SqliteDataLiterals.MarkerTable),
            new Where([new Compare(CompareOp.Equal, new ColumnRef(SqliteDataLiterals.MarkerKeyColumn), new StringLit(key))]));
        return (await RowsAsync(query, ct)).Select(r => (string?)r[0]).FirstOrDefault();
    }

    public async Task<CatalogView?> ViewAsync(string view, CancellationToken ct = default)
    {
        var found = (await ObjectsAsync(ct)).FirstOrDefault(o =>
            o.Type == SqliteDataLiterals.ViewType && string.Equals(o.Name, view, StringComparison.OrdinalIgnoreCase));
        return found is null ? null : new CatalogView(found.Name, await MarkerAsync(MarkerKey(SqliteDataLiterals.ViewKind, found.Name), ct));
    }

    public async Task<bool> TableExistsAsync(string table, CancellationToken ct = default) =>
        TableOf(await ObjectsAsync(ct), table) is not null;

    public async Task<IReadOnlyList<CatalogColumn>> ColumnsAsync(string table, CancellationToken ct = default) =>
        (await TableColumnsAsync(table, ct))
            .Select(c => new CatalogColumn(c.Name, c.Type, !c.NotNull && c.KeyPosition == 0, c.KeyPosition))
            .ToList();

    public async Task<IReadOnlyList<CatalogIndex>> IndexesAsync(string table, CancellationToken ct = default)
    {
        var indexes = new List<CatalogIndex>();
        var definitions = (await ObjectsAsync(ct)).Where(o => o.Type == SqliteDataLiterals.IndexType).ToDictionary(o => o.Name, o => o.Sql, StringComparer.Ordinal);
        foreach (var row in await RowsAsync(new SqlitePragma("index_list", table), ct))
        {
            if ((string)row[3]! != SqliteDataLiterals.ExplicitIndexOrigin) continue;
            var name = (string)row[1]!;
            var terms = (await RowsAsync(new SqlitePragma("index_xinfo", name), ct))
                .Where(c => Int(c[5]) == 1)
                .OrderBy(c => Int(c[0]))
                .Select(c => (string?)c[2] ?? SqliteDataLiterals.ExpressionTerm)
                .ToList();
            indexes.Add(new CatalogIndex(name, Int(row[2]) != 0, Int(row[4]) != 0, terms, definitions.GetValueOrDefault(name, "")));
        }
        return indexes.OrderBy(i => i.Name, StringComparer.Ordinal).ToList();
    }

    private static ReferentialAction ActionOfKey(string table, IReadOnlyList<string> columns, string text)
    {
        try
        {
            return SqliteSqlText.ActionOf(text);
        }
        catch (NotSupportedException refusal)
        {
            throw new NotSupportedException(
                $"the foreign key of table '{table}' over ({string.Join(", ", columns)}) uses the action '{text}'; "
                + "the catalog does not model it, so this table's foreign keys cannot be listed.",
                refusal);
        }
    }

    public async Task<IReadOnlyList<CatalogForeignKey>> ForeignKeysAsync(string table, CancellationToken ct = default)
    {
        var tableSql = TableOf(await ObjectsAsync(ct), table);
        var named = tableSql is null
            ? []
            : SqliteSqlText.ParseTable(tableSql.Sql).Definitions
                .Where(SqliteSqlText.IsForeignKey)
                .Select(SqliteSqlText.TryParseForeignKey)
                .OfType<ForeignKeyText>()
                .ToList();
        var keys = new List<CatalogForeignKey>();
        var groups = (await RowsAsync(new SqlitePragma("foreign_key_list", table), ct)).GroupBy(r => Int(r[0]));
        foreach (var group in groups)
        {
            var rows = group.OrderBy(r => Int(r[1])).ToList();
            var columns = rows.Select(r => (string)r[3]!).ToList();
            var parent = (string)rows[0][2]!;
            var referenced = rows.Select(r => (string?)r[4]).ToList();
            if (referenced.Any(r => r is null))
                referenced = (await TableColumnsAsync(parent, ct)).Where(c => c.KeyPosition > 0).OrderBy(c => c.KeyPosition).Select(c => (string?)c.Name).ToList();
            var name = named.FirstOrDefault(n =>
                string.Equals(n.ReferencedTable, parent, StringComparison.OrdinalIgnoreCase)
                && n.Columns.SequenceEqual(columns, StringComparer.OrdinalIgnoreCase))?.Name;
            keys.Add(new CatalogForeignKey(
                name,
                columns,
                parent,
                referenced.Select(r => r!).ToList(),
                ActionOfKey(table, columns, (string)rows[0][6]!),
                ActionOfKey(table, columns, (string)rows[0][5]!)));
        }
        return keys.OrderBy(k => k.Name, StringComparer.Ordinal).ThenBy(k => string.Join(",", k.Columns), StringComparer.Ordinal).ToList();
    }
}
