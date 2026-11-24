using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Nodes;

public sealed class DdlOperationTests
{
    private static readonly ColumnRef A = new("a");

    private static readonly ColumnRef B = new("b");

    private static MigrationBuilder Builder() => new(PostgresProvider.Instance);

    private static Node Only(MigrationBuilder builder) => Assert.Single(builder.Nodes);

    private static string Text(MigrationBuilder builder) => Assert.Single(builder.Operations);

    [Fact(DisplayName = "every builder operation records exactly one node of its own type, and the node is data: two builders given the same calls hold equal trees")]
    public void Each_operation_records_one_node()
    {
        var cases = new (Action<MigrationBuilder> Call, Node Expected)[]
        {
            (b => b.Raw("postgres", "SELECT 1"), new RawSql("postgres", "SELECT 1")),
            (b => b.DropTable("t"), new DropTable("t")),
            (b => b.AddColumn("t", "c", ColumnType.Text), new AddColumn("t", new ColumnDef("c", new TypeRef(ColumnType.Text)))),
            (b => b.AddColumn("t", "c", ColumnType.Int32, false, Defaults.Number(0)),
                new AddColumn("t", new ColumnDef("c", new TypeRef(ColumnType.Int32), false, false, new IntLit(0)))),
            (b => b.AddDecimalColumn("t", "c", 12, 4), new AddColumn("t", new ColumnDef("c", new TypeRef(ColumnType.Decimal, 12, 4)))),
            (b => b.AddJsonbColumn("t", "c"), new AddColumn("t", new ColumnDef("c", new TypeRef(ColumnType.Json)))),
            (b => b.AddJsonbColumn("t", "c", false),
                new AddColumn("t", new ColumnDef("c", new TypeRef(ColumnType.Json), false, false, new Cast(new StringLit("{}"), CastType.Json)))),
            (b => b.DropColumn("t", "c"), new DropColumn("t", "c")),
            (b => b.RenameColumn("t", "a", "b"), new RenameColumn("t", "a", "b")),
            (b => b.CreateIndex("i", "t", "a", "b"), new CreateIndex("i", "t", [A, B])),
            (b => b.CreateUniqueIndex("i", "t", "a"), new CreateIndex("i", "t", [A], true)),
            (b => b.DropIndex("i"), new DropIndex("i")),
            (b => b.AddForeignKey("fk", "t", "a", "o", "id"), new AddForeignKey("fk", "t", [A], "o", [new ColumnRef("id")])),
            (b => b.DropForeignKey("fk", "t"), new DropForeignKey("fk", "t")),
            (b => b.InsertData("t", ["a", "b"], ["x", 1]), new InsertData("t", [A, B], [new DataRow([new StringLit("x"), new IntLit(1)])])),
            (b => b.DeleteData("t", "a", "x", 2), new DeleteData("t", "a", [new StringLit("x"), new IntLit(2)])),
            (b => b.CreateTable("t", t => t.Column("a", ColumnType.Uuid, false, true)),
                new CreateTable("t", [new ColumnDef("a", new TypeRef(ColumnType.Uuid), false, true)])),
        };

        foreach (var (call, expected) in cases)
        {
            var first = Builder();
            call(first);
            var second = Builder();
            call(second);

            Assert.Equal(expected, Only(first));
            Assert.Equal(Only(first), Only(second));
        }
    }

    [Fact(DisplayName = "a builder keeps its operations in the order they were called")]
    public void Operations_keep_their_order()
    {
        var builder = Builder().DropTable("a").DropTable("b").DropTable("c");

        Assert.Equal(new Node[] { new DropTable("a"), new DropTable("b"), new DropTable("c") }, builder.Nodes);
        Assert.Equal(
            new[] { "DROP TABLE IF EXISTS \"a\"", "DROP TABLE IF EXISTS \"b\"", "DROP TABLE IF EXISTS \"c\"" },
            builder.Operations);
    }

    [Fact(DisplayName = "typed columns render the PostgreSQL spelling of each column type and each default, with no SQL text supplied by the caller")]
    public void Typed_columns_render_their_spelling()
    {
        var builder = Builder().CreateTable("notes", t => t
            .Column("id", ColumnType.Uuid, false, true)
            .Column("title", ColumnType.Text, false, defaultValue: Defaults.Text(""))
            .Column("stars", ColumnType.Int32, defaultValue: Defaults.Number(0))
            .Column("big", ColumnType.Int64)
            .Column("flag", ColumnType.Bool, false, defaultValue: Defaults.Flag(false))
            .Column("ratio", ColumnType.Double)
            .Column("day", ColumnType.Date)
            .Column("at", ColumnType.Timestamp, false, defaultValue: Defaults.Now)
            .DecimalColumn("amount", 12, 4, false)
            .JsonbColumn("doc", false));

        Assert.Equal(
            "CREATE TABLE IF NOT EXISTS \"notes\" (\"id\" uuid NOT NULL PRIMARY KEY, \"title\" text NOT NULL DEFAULT '', \"stars\" integer DEFAULT 0, "
            + "\"big\" bigint, \"flag\" boolean NOT NULL DEFAULT false, \"ratio\" double precision, \"day\" date, "
            + "\"at\" timestamp with time zone NOT NULL DEFAULT now(), \"amount\" numeric(12, 4) NOT NULL, \"doc\" jsonb NOT NULL DEFAULT '{}'::jsonb)",
            Text(builder));
    }

    [Fact(DisplayName = "an index renders its columns in order, a unique one says so, and a drop or a foreign key names what it removes or references")]
    public void Index_and_key_operations_render()
    {
        Assert.Equal("CREATE UNIQUE INDEX IF NOT EXISTS \"uq\" ON \"t\" (\"a\", \"b\")", Text(Builder().CreateUniqueIndex("uq", "t", "a", "b")));
        Assert.Equal("DROP INDEX IF EXISTS \"uq\"", Text(Builder().DropIndex("uq")));
        Assert.Equal(
            "ALTER TABLE \"t\" ADD CONSTRAINT \"fk\" FOREIGN KEY (\"a\") REFERENCES \"o\" (\"id\")",
            Text(Builder().AddForeignKey("fk", "t", "a", "o", "id")));
        Assert.Equal("ALTER TABLE \"t\" DROP CONSTRAINT \"fk\"", Text(Builder().DropForeignKey("fk", "t")));
    }

    [Fact(DisplayName = "data operations render literals inline, quote-doubled, and keep a null as NULL")]
    public void Data_operations_render_literals()
    {
        Assert.Equal(
            "INSERT INTO \"t\" (\"a\", \"b\") VALUES ('it''s', NULL), (true, -7)",
            Text(Builder().InsertData("t", ["a", "b"], ["it's", null], [true, -7])));
        Assert.Equal("DELETE FROM \"t\" WHERE \"a\" IN ('x', 2)", Text(Builder().DeleteData("t", "a", "x", 2)));
    }

    [Fact(DisplayName = "a Guid value renders as a quoted lowercase D-format string, and an insert without rows "
        + "is refused before a node is recorded")]
    public void A_guid_renders_quoted_and_an_insert_without_rows_is_refused()
    {
        var id = Guid.Parse("a0000000-0000-0000-0000-00000000000a");

        Assert.Equal(
            "INSERT INTO \"t\" (\"g\") VALUES ('a0000000-0000-0000-0000-00000000000a')",
            Text(Builder().InsertData("t", ["g"], [id])));

        var builder = Builder();

        Assert.Throws<ArgumentException>(() => builder.InsertData("t", ["g"]));
        Assert.Empty(builder.Nodes);
    }

    [Fact(DisplayName = "a number renders with its invariant text whatever the thread culture is")]
    public void Numbers_render_invariantly()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            var comma = (System.Globalization.CultureInfo)System.Globalization.CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            Thread.CurrentThread.CurrentCulture = comma;
            Assert.Equal("INSERT INTO \"t\" (\"d\") VALUES (1.5), (2.25)", Text(Builder().InsertData("t", ["d"], [1.5], [2.25m])));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact(DisplayName = "DeleteData without a value is refused before any node is recorded")]
    public void Delete_without_values_is_refused()
    {
        var builder = Builder();

        Assert.Throws<ArgumentException>(() => builder.DeleteData("t", "a"));
        Assert.Empty(builder.Nodes);
    }

    [Fact(DisplayName = "the legacy constructors render through the registered PostgreSQL provider, and name the missing package when none is registered")]
    public void Legacy_constructors_resolve_the_registered_provider()
    {
        var viaDialect = new MigrationBuilder(new PgDdlDialect()).DropTable("t");
        var viaDefault = new MigrationBuilder().DropTable("t");

        Assert.Equal(viaDialect.Operations, viaDefault.Operations);
        Assert.Equal(viaDialect.Operations, viaDialect.Render(PostgresProvider.Instance));
        var error = Assert.Throws<InvalidOperationException>(() => ProviderRegistry.Require("oracle"));
        Assert.Equal("provider 'oracle' is not installed; add Neemle.Orm.Lite.Oracle and register its provider.", error.Message);
    }

    [Fact(DisplayName = "a table builder renders its columns as the comma-joined list the CREATE TABLE node wraps")]
    public void A_table_builder_renders_its_column_list()
    {
        var table = new TableBuilder().Column("id", ColumnType.Uuid, false, true).Column("n", ColumnType.Int32, defaultValue: Defaults.Number(1));

        Assert.Equal("\"id\" uuid NOT NULL PRIMARY KEY, \"n\" integer DEFAULT 1", table.Render());
        Assert.Equal(2, table.Columns.Count);
    }

    [Fact(DisplayName = "a raw statement scoped to another provider is refused with both provider names, an unscoped one and one scoped to the compiling provider render verbatim")]
    public void Raw_statements_are_scoped()
    {
        var scoped = Builder().Raw("sqlite", "PRAGMA x");

        var error = Assert.Throws<InvalidOperationException>(() => scoped.Operations);

        Assert.Equal("raw SQL is scoped to provider 'sqlite' and cannot run on provider 'postgres'.", error.Message);
        Assert.Equal("  SELECT 1 ", Text(Builder().Add(new RawSql(null, "  SELECT 1 "))));
        Assert.Equal("SELECT 2", Text(Builder().Raw("postgres", "SELECT 2")));
    }

    [Fact(DisplayName = "a capability the provider declares None is refused by name before any text is written")]
    public void A_missing_capability_is_refused()
    {
        var nothing = new NothingProvider();

        var error = Assert.Throws<CapabilityNotSupportedException>(
            () => DdlCompiler.Compile(new CreateIndex("i", "t", [A], false, new Where([new IsNull(A)])), nothing));

        Assert.Equal(Capability.PartialIndex, error.Capability);
        Assert.Equal(nameof(CreateIndex), error.Node);
        Assert.Equal("nothing", error.Provider);
    }

    private sealed class NothingProvider : ISqlProvider
    {
        public string Id => "nothing";

        public CapabilitySet Capabilities { get; } = CapabilitySet.Nothing();

        public ISerializer CreateSerializer() => new PostgresSerializer();
    }
}
