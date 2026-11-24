using System.Data.Common;
using System.Globalization;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Postgres;

public sealed class PostgresCatalog(ISqlProvider provider, DbConnection connection, DbTransaction? transaction = null)
    : ISchemaCatalog
{
    public static string ForeignKeyCode(ReferentialAction action) => PostgresLowering.ForeignKeyCode(action);

    private sealed record Attribute(short Number, string Name, string Type, bool NotNull);

    private static ReferentialAction ActionOf(string code) => code switch
    {
        "c" => ReferentialAction.Cascade,
        "n" => ReferentialAction.SetNull,
        "a" => ReferentialAction.NoAction,
        "r" => ReferentialAction.Restrict,
        _ => throw new NotSupportedException($"the referential action code '{code}' has no ReferentialAction."),
    };

    private static PgCall ToRegclass(string table) => new("to_regclass", NodeList.Of([PostgresNames.Regclass(table)]));

    private static PgName Name(string text) => new(text);

    private static Compare Eq(Node left, Node right) => new(CompareOp.Equal, left, right);

    private async Task<List<object?[]>> RowsAsync(Node query, CancellationToken ct)
    {
        var statement = StatementCompiler.Compile(query, provider, StatementKind.Read);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = statement.Text;
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

    public async Task<CatalogView?> ViewAsync(string view, CancellationToken ct = default)
    {
        var query = new Select(
            [new PgCall("obj_description", NodeList.Of([ToRegclass(view), new StringLit("pg_class")]))],
            new TableRef("pg_class", "c"),
            new Where([Eq(Name("c.oid"), ToRegclass(view)), Eq(Name("c.relkind"), new StringLit("v"))]));
        var rows = await RowsAsync(query, ct);
        return rows.Count == 0 ? null : new CatalogView(view, (string?)rows[0][0]);
    }

    internal static Node TableQuery(string table)
    {
        var kinds = new InList(Name("c.relkind"), [new StringLit("r"), new StringLit("p")]);
        var query = new PgQuery(
            [new IntLit(1)],
            new PgRelation("pg_class", "c"),
            new Where([Eq(Name("c.oid"), ToRegclass(table)), kinds]));
        return new Select([new PgExists(false, query)]);
    }

    public async Task<bool> TableExistsAsync(string table, CancellationToken ct = default) =>
        (bool)(await RowsAsync(TableQuery(table), ct))[0][0]!;

    private Task<List<Attribute>> AttributesAsync(string table, CancellationToken ct) =>
        AttributesOfAsync(ToRegclass(table), ct);

    // The columns of the relation a node names: by name for a table the caller asked for, by the oid the
    // catalog itself holds (a constraint's confrelid) for a table it reached through a foreign key.
    private async Task<List<Attribute>> AttributesOfAsync(Node relation, CancellationToken ct)
    {
        var format = new PgCall("format_type", NodeList.Of([Name("a.atttypid"), Name("a.atttypmod")]));
        var query = new Select(
            [Name("a.attnum"), Name("a.attname"), format, Name("a.attnotnull")],
            new TableRef("pg_attribute", "a"),
            new Where([
                Eq(Name("a.attrelid"), relation),
                new Compare(CompareOp.Greater, Name("a.attnum"), new IntLit(0)),
                new PgNegate(Name("a.attisdropped")),
            ]),
            new OrderBy([new OrderTerm(Name("a.attnum"), false)]));
        var rows = await RowsAsync(query, ct);
        return rows.Select(r => new Attribute((short)r[0]!, (string)r[1]!, (string)r[2]!, (bool)r[3]!)).ToList();
    }

    // The parent of one foreign key of a table, as an oid read from the constraint, so a parent in a schema
    // that is not on the search path, or one a same-named table of an earlier schema hides, is the right one.
    private static PgScalar ParentOf(string table, string constraint) => new(new PgQuery(
        [Name("p.confrelid")],
        new PgRelation("pg_constraint", "p"),
        new Where([
            Eq(Name("p.conrelid"), ToRegclass(table)),
            Eq(Name("p.conname"), new StringLit(constraint)),
            Eq(Name("p.contype"), new StringLit("f")),
        ])));

    private static IReadOnlyList<short> Numbers(string vector) =>
        vector.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(short.Parse).ToList();

    // The key columns of an index: indkey lists them and then the INCLUDE columns, indnkeyatts counts the key ones.
    private static IReadOnlyList<short> KeyNumbers(object? vector, object? keyCount) =>
        Numbers((string)vector!).Take(Convert.ToInt32(keyCount, CultureInfo.InvariantCulture)).ToList();

    public async Task<IReadOnlyList<CatalogColumn>> ColumnsAsync(string table, CancellationToken ct = default)
    {
        var attributes = await AttributesAsync(table, ct);
        var key = new Select(
            [new PgCast(Name("i.indkey"), "text"), new PgCast(Name("i.indnkeyatts"), "int4")],
            new TableRef("pg_index", "i"),
            new Where([Eq(Name("i.indrelid"), ToRegclass(table)), Name("i.indisprimary")]));
        var primary = (await RowsAsync(key, ct)).Select(r => KeyNumbers(r[0], r[1])).FirstOrDefault() ?? [];
        return attributes
            .Select(a => new CatalogColumn(a.Name, a.Type, !a.NotNull, primary.ToList().IndexOf(a.Number) + 1))
            .ToList();
    }

    private static PgExists OwnedByConstraint()
    {
        var kinds = new InList(Name("k.contype"), [new StringLit("p"), new StringLit("u")]);
        return new PgExists(false, new PgQuery(
            [new IntLit(1)],
            new PgRelation("pg_constraint", "k"),
            new Where([Eq(Name("k.conindid"), Name("i.indexrelid")), kinds])));
    }

    public async Task<IReadOnlyList<CatalogIndex>> IndexesAsync(string table, CancellationToken ct = default)
    {
        var attributes = (await AttributesAsync(table, ct)).ToDictionary(a => a.Number, a => a.Name);
        var query = new Select(
            [
                new PgScalar(new PgQuery([Name("c.relname")], new PgRelation("pg_class", "c"), new Where([Eq(Name("c.oid"), Name("i.indexrelid"))]))),
                Name("i.indisunique"),
                new IsNotNull(Name("i.indpred")),
                new PgCast(Name("i.indkey"), "text"),
                OwnedByConstraint(),
                new PgCall("pg_get_indexdef", NodeList.Of([Name("i.indexrelid")])),
                new PgCast(Name("i.indnkeyatts"), "int4"),
            ],
            new TableRef("pg_index", "i"),
            new Where([Eq(Name("i.indrelid"), ToRegclass(table)), new PgNegate(Name("i.indisprimary"))]));
        string ColumnOf(short number) =>
            attributes.TryGetValue(number, out var name) ? name : PostgresDdlDataLiterals.ExpressionTerm;
        return (await RowsAsync(query, ct))
            .Where(r => !(bool)r[4]!)
            .Select(r => new CatalogIndex(
                (string)r[0]!,
                (bool)r[1]!,
                (bool)r[2]!,
                KeyNumbers(r[3], r[6]).Select(ColumnOf).ToList(),
                (string)r[5]!))
            .OrderBy(i => i.Name, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<IReadOnlyList<CatalogForeignKey>> ForeignKeysAsync(string table, CancellationToken ct = default)
    {
        var query = new Select(
            [
                Name("c.conname"),
                Name("c.conkey"),
                Name("c.confkey"),
                new PgScalar(new PgQuery([Name("t.relname")], new PgRelation("pg_class", "t"), new Where([Eq(Name("t.oid"), Name("c.confrelid"))]))),
                new PgCast(Name("c.confdeltype"), "text"),
                new PgCast(Name("c.confupdtype"), "text"),
            ],
            new TableRef("pg_constraint", "c"),
            new Where([Eq(Name("c.conrelid"), ToRegclass(table)), Eq(Name("c.contype"), new StringLit("f"))]));
        var own = (await AttributesAsync(table, ct)).ToDictionary(a => a.Number, a => a.Name);
        var keys = new List<CatalogForeignKey>();
        foreach (var row in await RowsAsync(query, ct))
        {
            var parent = (string)row[3]!;
            var parentColumns = await AttributesOfAsync(ParentOf(table, (string)row[0]!), ct);
            var parentNames = parentColumns.ToDictionary(a => a.Number, a => a.Name);
            keys.Add(new CatalogForeignKey(
                (string)row[0]!,
                ((short[])row[1]!).Select(n => own[n]).ToList(),
                parent,
                ((short[])row[2]!).Select(n => parentNames[n]).ToList(),
                ActionOf((string)row[4]!),
                ActionOf((string)row[5]!)));
        }
        return keys.OrderBy(k => k.Name, StringComparer.Ordinal).ToList();
    }
}
