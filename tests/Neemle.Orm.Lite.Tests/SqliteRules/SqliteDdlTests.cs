using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sqlite;
using Neemle.Orm.Lite.Tests.Nodes;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SqliteRules;

public sealed class SqliteDdlTests
{
    private static readonly ColumnRef A = new("a");

    private static string Text(Node op) => Assert.Single(DdlCompiler.Compile(op, SqliteProvider.Instance)).Text;

    private static ColumnDef Col(string name, ColumnType type, bool nullable = true, bool key = false, Node? fallback = null) =>
        new(name, new TypeRef(type), nullable, key, fallback);

    [Fact(DisplayName = "a table is written with SQLite's own column spellings, a primary key that refuses null, defaults, and its inline foreign keys")]
    public void Create_table_text()
    {
        var table = new CreateTable(
            "t",
            [
                Col("id", ColumnType.Uuid, true, true),
                Col("n", ColumnType.Int32, false, false, Defaults.Number(0)),
                Col("big", ColumnType.Int64),
                Col("flag", ColumnType.Bool, false, false, Defaults.Flag(false)),
                Col("ratio", ColumnType.Double),
                new ColumnDef("price", new TypeRef(ColumnType.Decimal, 12, 4)),
                Col("at", ColumnType.Timestamp, false, false, Defaults.Now),
                Col("day", ColumnType.Date),
                Col("label", ColumnType.Text, true, false, Defaults.Text("it's")),
            ],
            [new ForeignKeyDef("fk_t_o", [A], "o", [new ColumnRef("id")], ReferentialAction.Cascade, ReferentialAction.NoAction)]);

        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS \"t\" (\"id\" text NOT NULL PRIMARY KEY, \"n\" integer NOT NULL DEFAULT 0, \"big\" integer, \"flag\" boolean NOT NULL DEFAULT false, "
            + "\"ratio\" real, \"price\" text COLLATE orm_decimal CHECK (orm_decimal_fits(\"price\", 12, 4)), \"at\" text NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%f', 'now') || '0000Z'), \"day\" text, \"label\" text DEFAULT 'it''s', "
            + "CONSTRAINT \"fk_t_o\" FOREIGN KEY (\"a\") REFERENCES \"o\" (\"id\") ON UPDATE NO ACTION ON DELETE CASCADE)",
            Text(table));
    }

    [Fact(DisplayName = "the plain operations are one statement each: rename, indexes with a predicate and a lowered column, drop table, and a native added or dropped column without IF clauses")]
    public void Plain_operation_text()
    {
        Assert.Equal("ALTER TABLE \"t\" RENAME COLUMN \"a\" TO \"b\"", Text(new RenameColumn("t", "a", "b")));
        Assert.Equal(
            "CREATE UNIQUE INDEX IF NOT EXISTS \"ix\" ON \"t\" (\"a\", lower(\"b\")) WHERE \"d\" IS NULL",
            Text(new CreateIndex("ix", "t", [A, new Call(Fn.Lower, [new ColumnRef("b")])], true, new Where([new IsNull(new ColumnRef("d"))]))));
        Assert.Equal("DROP INDEX IF EXISTS \"ix\"", Text(new DropIndex("ix")));
        Assert.Equal("DROP TABLE IF EXISTS \"t\"", Text(new DropTable("t")));
        Assert.Equal("ALTER TABLE \"t\" ADD COLUMN \"c\" text DEFAULT 'x'", Text(new AddColumn("t", Col("c", ColumnType.Text, true, false, Defaults.Text("x")), false)));
        Assert.Equal("ALTER TABLE \"t\" DROP COLUMN \"c\"", Text(new DropColumn("t", "c", false)));
        Assert.Equal(
            "INSERT INTO \"t\" (\"a\") VALUES ('x'), (NULL)",
            Text(new InsertData("t", [A], [new DataRow([new StringLit("x")]), new DataRow([new NullLit()])])));
        Assert.Equal("DELETE FROM \"t\" WHERE \"a\" IN ('x', 1)", Text(new DeleteData("t", "a", [new StringLit("x"), new IntLit(1)])));
    }

    [Fact(DisplayName = "the rebuild statements are written from nodes: the temporary table, the row copy, the rename, the drops of views and triggers, and the pragma")]
    public void Rebuild_statement_text()
    {
        var provider = SqliteProvider.Instance;
        string Of(Node node) => StatementCompiler.Compile(node, provider, StatementKind.Ddl).Text;

        Assert.Equal(
            "CREATE TABLE \"t__orm_rebuild\" (\"a\" text, CONSTRAINT \"k\" UNIQUE (\"a\")) STRICT",
            Of(new SqliteCreateTableFrom("t__orm_rebuild", [new SqliteStored("\"a\" text"), new SqliteStored("CONSTRAINT \"k\" UNIQUE (\"a\")")], "STRICT")));
        Assert.Equal("INSERT INTO \"t__orm_rebuild\" (\"a\", \"b\") SELECT \"a\", \"b\" FROM \"t\"", Of(new SqliteCopyRows("t", "t__orm_rebuild", [A, new ColumnRef("b")])));
        Assert.Equal("ALTER TABLE \"t__orm_rebuild\" RENAME TO \"t\"", Of(new SqliteRenameTable("t__orm_rebuild", "t")));
        Assert.Equal("DROP VIEW IF EXISTS \"v\"", Of(new SqliteDropObject("VIEW", "v")));
        Assert.Equal("DROP TRIGGER IF EXISTS \"g\"", Of(new SqliteDropObject("TRIGGER", "g")));
        Assert.Equal("PRAGMA foreign_keys = OFF", Of(new SqlitePragma("foreign_keys", null, "OFF")));
        Assert.Equal("PRAGMA table_xinfo(\"t\")", Of(new SqlitePragma("table_xinfo", "t")));
        Assert.Equal(
            "SELECT 1 FROM \"t\" WHERE \"a\" IS NOT NULL AND \"b\" IS NOT NULL GROUP BY \"a\", lower(\"b\") HAVING count(*) > 1 LIMIT 1",
            Of(new SqliteDuplicateKeys("t", [A, new Call(Fn.Lower, [new ColumnRef("b")])], [A, new ColumnRef("b")])));
    }

    [Fact(DisplayName = "a decimal column is text under the decimal collation guarded by a fit check, one above the exact range or with a scale over its precision is refused by name, and a JSON column is text guarded by a validity check that its empty-document default satisfies")]
    public void Unspellable_types_are_refused()
    {
        var tooPrecise = Assert.Throws<NotSupportedException>(() =>
            Text(new CreateTable("t", [new ColumnDef("p", new TypeRef(ColumnType.Decimal, 29, 4))])));
        var tooScaled = Assert.Throws<NotSupportedException>(() =>
            Text(new CreateTable("t", [new ColumnDef("p", new TypeRef(ColumnType.Decimal, 4, 5))])));

        Assert.Contains("precision 29", tooPrecise.Message, StringComparison.Ordinal);
        Assert.Contains("scale 5", tooScaled.Message, StringComparison.Ordinal);
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS \"t\" (\"p\" text COLLATE orm_decimal CHECK (orm_decimal_fits(\"p\", 0, 0)))",
            Text(new CreateTable("t", [Col("p", ColumnType.Decimal)])));
        Assert.Equal("CREATE TABLE IF NOT EXISTS \"t\" (\"j\" text CHECK (json_valid(\"j\")))", Text(new CreateTable("t", [Col("j", ColumnType.Json)])));
        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS \"t\" (\"j\" text CHECK (json_valid(\"j\")) NOT NULL DEFAULT '{}')",
            Text(new CreateTable("t", [new ColumnDef("j", new TypeRef(ColumnType.Json), false, false, Defaults.EmptyDocument)])));
        Assert.Equal(
            "ALTER TABLE \"t\" ADD COLUMN \"j\" text CHECK (json_valid(\"j\")) NOT NULL DEFAULT '{}'",
            Text(new AddColumn("t", new ColumnDef("j", new TypeRef(ColumnType.Json), false, false, Defaults.EmptyDocument), false)));
        Assert.Equal(
            "ALTER TABLE \"t\" ADD COLUMN \"p\" text COLLATE orm_decimal CHECK (orm_decimal_fits(\"p\", 19, 4))",
            Text(new AddColumn("t", new ColumnDef("p", new TypeRef(ColumnType.Decimal, 19, 4)), false)));
    }

    [Fact(DisplayName = "a node that needs the live catalog has no static statement and says which runner plans it")]
    public void Planned_nodes_have_no_static_statement()
    {
        Node[] planned =
        [
            new AddColumn("t", Col("c", ColumnType.Text)),
            new DropColumn("t", "c"),
            new AddForeignKey("fk", "t", [A], "o", [new ColumnRef("id")]),
            new DropForeignKey("fk", "t"),
            new ConvertForeignKeyAction("t", "fk", [A], "o", [new ColumnRef("id")], ReferentialAction.Cascade, ReferentialAction.NoAction, ReferentialAction.NoAction),
            new ConvertToLiveOnlyIndex("t", [A], "deleted_at", "uq", "ix", false, "v1", false),
            new SoftDeleteColumn("t", "v1", false),
            new RowHistoryFunction("a.b"),
            new RowHistoryInstall("a.b", []),
            new AddColumn("t", Col("c", ColumnType.Text, false), false),
            new AddColumn("t", Col("c", ColumnType.Timestamp, true, false, Defaults.Now), false),
        ];

        foreach (var node in planned)
        {
            var refusal = Assert.Throws<NotSupportedException>(() => DdlCompiler.Compile(node, SqliteProvider.Instance));
            Assert.Contains(node.Name, refusal.Message, StringComparison.Ordinal);
            Assert.Contains("MigrationRunner", refusal.Message, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "every node of the roster has a recorded disposition on SQLite, and the refused ones are refused by capability or by name before a statement")]
    public void Every_node_has_a_disposition()
    {
        var planner = SqliteProvider.Instance.Planner;

        foreach (var node in DdlNodeRoster.Samples)
        {
            var disposition = SqliteDispositions.Of(node);
            if (node is RawSql { ProviderId: "postgres" })
                Assert.Throws<InvalidOperationException>(() => planner.Validate([node]));
            else if (disposition == Disposition.Refused)
                Assert.ThrowsAny<NotSupportedException>(() => planner.Validate([node]));
            else
                planner.Validate([node]);
        }
        Assert.Equal(
            [typeof(HistoryTableNode), typeof(SessionLock)],
            DdlNodeRoster.Samples.Where(n => SqliteDispositions.Of(n) == Disposition.Refused).Select(n => n.GetType()).OrderBy(t => t.Name, StringComparer.Ordinal));
    }

    [Fact(DisplayName = "the capabilities SQLite declares for DDL are native for a partial index, emulated for constraint changes, markers and row history, and none for session locks")]
    public void Declared_levels()
    {
        var capabilities = SqliteProvider.Instance.Capabilities;

        Assert.Equal(Support.Native, capabilities.Of(Capability.PartialIndex));
        Assert.Equal(Support.Emulated, capabilities.Of(Capability.AlterConstraint));
        Assert.Equal(Support.Emulated, capabilities.Of(Capability.ObjectMarker));
        Assert.Equal(Support.Emulated, capabilities.Of(Capability.RowHistoryTriggers));
        Assert.Equal(Support.None, capabilities.Of(Capability.SessionLock));
    }
}
