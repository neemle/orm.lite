using System.Data.Common;
using System.Globalization;
using Neemle.Orm.Lite.Nodes;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public abstract class MigrationSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    protected TFixture Fixture { get; } = fixture;

    protected OrmContext Ctx => Fixture.Context;

    protected static ColumnDef Col(string name, ColumnType type, bool nullable = true, bool key = false, Node? fallback = null) =>
        new(name, new TypeRef(type), nullable, key, fallback);

    protected static NodeList Cols(params string[] names) => NodeList.Of(names.Select(n => (Node)new ColumnRef(n)));

    protected static CreateTable Table(string name, params ColumnDef[] columns) => new(name, NodeList.Of(columns));

    protected static ForeignKeyDef Link(string name, string column, string parent, ReferentialAction? onDelete = null) =>
        new(name, Cols(column), parent, Cols("id"), onDelete);

    protected Task RunAsync(string version, params Node[] operations) =>
        MigrationRunner.RunAsync(Ctx.DataSource, Ctx.Provider, [MigrationContribution.Of(version, Reversibility.Reversible, operations)]);

    protected Task ApplyAsync(params Node[] operations) =>
        MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, operations);

    protected Task ExecAsync(string statement) => Fixture.ExecuteAsync(statement);

    protected async Task FailsAsync(string statement) =>
        await Assert.ThrowsAnyAsync<DbException>(() => Fixture.ExecuteAsync(statement));

    protected async Task<string> RowsAsync(string statement)
    {
        var rows = await Fixture.RowsAsync(statement);
        return string.Join(" ; ", rows.Select(r => string.Join("|", r.Select(v => v is null ? "~" : Convert.ToString(v, CultureInfo.InvariantCulture)))));
    }

    protected static string Family(string native)
    {
        var type = native.ToLowerInvariant();
        if (type.Contains("bool", StringComparison.Ordinal)) return "bool";
        if (type.Contains("int", StringComparison.Ordinal) && !type.Contains("interval", StringComparison.Ordinal)) return "int";
        if (type.Contains("real", StringComparison.Ordinal) || type.Contains("double", StringComparison.Ordinal) || type.Contains("numeric", StringComparison.Ordinal)) return "real";
        return "text";
    }

    protected async Task<string> DescribeAsync(string table)
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        var catalog = Ctx.Provider.CreateCatalog(connection);
        if (!await catalog.TableExistsAsync(table)) return "absent";
        var columns = await catalog.ColumnsAsync(table);
        var indexes = await catalog.IndexesAsync(table);
        var keys = await catalog.ForeignKeysAsync(table);
        return "columns: " + string.Join(", ", columns.Select(c => $"{c.Name}:{Family(c.NativeType)}:{(c.Nullable ? "null" : "notnull")}:pk{c.KeyPosition}"))
            + "\nindexes: " + (indexes.Count == 0 ? "-" : string.Join(", ", indexes.Select(i =>
                $"{i.Name}({(i.Unique ? "unique" : "plain")}{(i.Partial ? ",partial" : "")})[{string.Join(",", i.Terms)}]")))
            + "\nforeign keys: " + (keys.Count == 0 ? "-" : string.Join(", ", keys.Select(k =>
                $"{k.Name}[{string.Join(",", k.Columns)}]->{k.ReferencedTable}[{string.Join(",", k.ReferencedColumns)}] delete={k.OnDelete} update={k.OnUpdate}")));
    }

    private static string ManyRows(string table, string columns, int from, int count, Func<int, string> row) =>
        "INSERT INTO \"" + table + "\" (" + columns + ") VALUES " + string.Join(", ", Enumerable.Range(from, count).Select(i => "(" + row(i) + ")"));

    [SkippableFact(DisplayName = "a table is created with typed columns, a default and an inline foreign key, and the catalog reads back the same schema on both providers")]
    public async Task Create_table_with_an_inline_foreign_key()
    {
        Fixture.RequireAvailable();

        await RunAsync(
            "0100_create",
            Table("mc_parent", Col("id", ColumnType.Text, false, true), Col("name", ColumnType.Text, false)),
            new CreateTable(
                "mc_child",
                NodeList.Of([Col("id", ColumnType.Text, false, true), Col("parent_id", ColumnType.Text), Col("note", ColumnType.Text, true, false, Defaults.Text("n"))]),
                [Link("fk_mc_child_parent", "parent_id", "mc_parent", ReferentialAction.Cascade)]));

        Assert.Equal(
            "columns: id:text:notnull:pk1, name:text:notnull:pk0\nindexes: -\nforeign keys: -",
            await DescribeAsync("mc_parent"));
        Assert.Equal(
            "columns: id:text:notnull:pk1, parent_id:text:null:pk0, note:text:null:pk0\nindexes: -\n"
            + "foreign keys: fk_mc_child_parent[parent_id]->mc_parent[id] delete=Cascade update=NoAction",
            await DescribeAsync("mc_child"));
        await ExecAsync("INSERT INTO \"mc_parent\" (\"id\", \"name\") VALUES ('p1', 'one')");
        await ExecAsync("INSERT INTO \"mc_child\" (\"id\", \"parent_id\") VALUES ('c1', 'p1')");
        await FailsAsync("INSERT INTO \"mc_child\" (\"id\", \"parent_id\") VALUES ('c2', 'missing')");
        await FailsAsync("INSERT INTO \"mc_parent\" (\"id\", \"name\") VALUES (NULL, 'x')");
        Assert.Equal("c1|n", await RowsAsync("SELECT \"id\", \"note\" FROM \"mc_child\""));
        await ExecAsync("DELETE FROM \"mc_parent\" WHERE \"id\" = 'p1'");
        Assert.Equal("", await RowsAsync("SELECT \"id\" FROM \"mc_child\""));
    }

    [SkippableFact(DisplayName = "a JSON column is created and added with the empty document as its default, takes a document and refuses text that is not one, on both providers")]
    public async Task Json_column()
    {
        Fixture.RequireAvailable();
        await RunAsync("0150_json", Table("mj", Col("id", ColumnType.Text, false, true), Col("doc", ColumnType.Json)));
        await ExecAsync("INSERT INTO \"mj\" (\"id\", \"doc\") VALUES ('a', '{\"k\":1}'), ('n', NULL)");

        await FailsAsync("INSERT INTO \"mj\" (\"id\", \"doc\") VALUES ('b', '{bad')");
        await RunAsync("0151_json", new AddColumn("mj", Col("extra", ColumnType.Json, false, false, Defaults.EmptyDocument)));
        await ExecAsync("INSERT INTO \"mj\" (\"id\") VALUES ('c')");
        await FailsAsync("INSERT INTO \"mj\" (\"id\", \"extra\") VALUES ('d', '[1,')");
        await FailsAsync("UPDATE \"mj\" SET \"extra\" = NULL WHERE \"id\" = 'a'");

        Assert.Equal("a ; c", await RowsAsync("SELECT \"id\" FROM \"mj\" WHERE \"doc\" IS NOT NULL OR \"id\" = 'c' ORDER BY \"id\""));
        Assert.Equal("a|{} ; c|{} ; n|{}", await RowsAsync("SELECT \"id\", \"extra\" FROM \"mj\" ORDER BY \"id\""));
        Assert.Equal(
            "columns: id:text:notnull:pk1, doc:text:null:pk0, extra:text:notnull:pk0\nindexes: -\nforeign keys: -",
            await DescribeAsync("mj"));
    }

    [SkippableFact(DisplayName = "added columns keep the existing rows, take their defaults, are skipped when they already exist, and an added column that must be rebuilt on SQLite reads the same")]
    public async Task Add_column_variants()
    {
        Fixture.RequireAvailable();
        await RunAsync("0200_a", Table("ma", Col("id", ColumnType.Text, false, true), Col("label", ColumnType.Text)));
        await ExecAsync("INSERT INTO \"ma\" (\"id\", \"label\") VALUES ('a', 'first'), ('b', 'second')");
        Node[] additions =
        [
            new AddColumn("ma", Col("extra", ColumnType.Text)),
            new AddColumn("ma", Col("qty", ColumnType.Int32, false, false, Defaults.Number(0))),
            new AddColumn("ma", Col("tag", ColumnType.Text, true, false, Defaults.Text("x"))),
            new AddColumn("ma", Col("flag", ColumnType.Bool, false, false, Defaults.Flag(true))),
            new AddColumn("ma", Col("added_at", ColumnType.Timestamp, false, false, Defaults.Now)),
        ];

        await RunAsync("0201_a", additions);
        await RunAsync("0202_a", additions);

        Assert.Equal(
            "columns: id:text:notnull:pk1, label:text:null:pk0, extra:text:null:pk0, qty:int:notnull:pk0, tag:text:null:pk0, flag:bool:notnull:pk0, added_at:text:notnull:pk0\nindexes: -\nforeign keys: -",
            await DescribeAsync("ma"));
        Assert.Equal("2", await RowsAsync("SELECT count(*) FROM \"ma\" WHERE \"flag\""));
        Assert.Equal("a|first|~|0|x ; b|second|~|0|x", await RowsAsync("SELECT \"id\", \"label\", \"extra\", \"qty\", \"tag\" FROM \"ma\" ORDER BY \"id\""));
        Assert.Equal("0", await RowsAsync("SELECT count(*) FROM \"ma\" WHERE \"added_at\" IS NULL"));
        await Assert.ThrowsAnyAsync<Exception>(() => ApplyAsync(new AddColumn("ma", Col("extra", ColumnType.Text), false)));
    }

    [SkippableFact(DisplayName = "an indexed column is dropped together with the indexes on it, a missing column is skipped, and the other columns, indexes and rows stay")]
    public async Task Drop_column_takes_its_indexes_with_it()
    {
        Fixture.RequireAvailable();
        await RunAsync(
            "0300_d",
            Table("md", Col("id", ColumnType.Text, false, true), Col("a", ColumnType.Text), Col("b", ColumnType.Text), Col("c", ColumnType.Text)),
            new CreateIndex("md_ab", "md", Cols("a", "b")),
            new CreateIndex("md_c", "md", Cols("c")));
        await ExecAsync("INSERT INTO \"md\" (\"id\", \"a\", \"b\", \"c\") VALUES ('1', 'x', 'y', 'z')");

        await RunAsync("0301_d", new DropColumn("md", "b"), new DropColumn("md", "never_existed"));

        Assert.Equal(
            "columns: id:text:notnull:pk1, a:text:null:pk0, c:text:null:pk0\nindexes: md_c(plain)[c]\nforeign keys: -",
            await DescribeAsync("md"));
        Assert.Equal("1|x|z", await RowsAsync("SELECT \"id\", \"a\", \"c\" FROM \"md\""));
        await Assert.ThrowsAnyAsync<Exception>(() => ApplyAsync(new DropColumn("md", "never_existed", false)));
    }

    [SkippableFact(DisplayName = "a renamed column keeps its rows and its index, which now names the new column")]
    public async Task Rename_column_keeps_data_and_index()
    {
        Fixture.RequireAvailable();
        await RunAsync("0400_r", Table("mr", Col("id", ColumnType.Text, false, true), Col("a", ColumnType.Text)), new CreateIndex("mr_a", "mr", Cols("a")));
        await ExecAsync("INSERT INTO \"mr\" (\"id\", \"a\") VALUES ('1', 'x')");

        await RunAsync("0401_r", new RenameColumn("mr", "a", "b"));

        Assert.Equal("columns: id:text:notnull:pk1, b:text:null:pk0\nindexes: mr_a(plain)[b]\nforeign keys: -", await DescribeAsync("mr"));
        Assert.Equal("x", await RowsAsync("SELECT \"b\" FROM \"mr\""));
    }

    [SkippableFact(DisplayName = "a live-only partial unique index refuses a live duplicate and admits soft-deleted twins, a lowered-column index folds case, and dropping an index twice is harmless")]
    public async Task Unique_partial_and_expression_indexes()
    {
        Fixture.RequireAvailable();
        var live = new Where([new IsNull(new ColumnRef("deleted_at"))]);

        await RunAsync(
            "0500_i",
            Table("mi", Col("id", ColumnType.Text, false, true), Col("tenant", ColumnType.Text), Col("title", ColumnType.Text), Col("deleted_at", ColumnType.Timestamp)),
            new CreateIndex("mi_live", "mi", Cols("tenant", "title"), true, live),
            new CreateIndex("mi_lower", "mi", [new Call(Fn.Lower, [new ColumnRef("title")])]),
            new CreateIndex("mi_plain", "mi", Cols("title")),
            new DropIndex("mi_plain"),
            new DropIndex("mi_plain"));

        Assert.Equal(
            "columns: id:text:notnull:pk1, tenant:text:null:pk0, title:text:null:pk0, deleted_at:text:null:pk0\n"
            + "indexes: mi_live(unique,partial)[tenant,title], mi_lower(plain)[<expression>]\nforeign keys: -",
            await DescribeAsync("mi"));
        await ExecAsync("INSERT INTO \"mi\" (\"id\", \"tenant\", \"title\") VALUES ('1', 't', 'One')");
        await FailsAsync("INSERT INTO \"mi\" (\"id\", \"tenant\", \"title\") VALUES ('2', 't', 'One')");
        await ExecAsync("INSERT INTO \"mi\" (\"id\", \"tenant\", \"title\", \"deleted_at\") VALUES ('3', 't', 'One', '2026-01-01T00:00:00Z')");
        await ExecAsync("INSERT INTO \"mi\" (\"id\", \"tenant\", \"title\", \"deleted_at\") VALUES ('4', 't', 'One', '2026-01-02T00:00:00Z')");
        await ExecAsync("INSERT INTO \"mi\" (\"id\", \"tenant\", \"title\") VALUES ('5', 'other', 'One')");
    }

    [SkippableFact(DisplayName = "a unique index refuses a duplicate on both providers")]
    public async Task A_unique_index_refuses_duplicates()
    {
        Fixture.RequireAvailable();
        await RunAsync("0510_u", Table("mu", Col("id", ColumnType.Text, false, true), Col("code", ColumnType.Text)), new CreateIndex("mu_code", "mu", Cols("code"), true));

        await ExecAsync("INSERT INTO \"mu\" (\"id\", \"code\") VALUES ('1', 'a')");

        await FailsAsync("INSERT INTO \"mu\" (\"id\", \"code\") VALUES ('2', 'a')");
        Assert.Equal("columns: id:text:notnull:pk1, code:text:null:pk0\nindexes: mu_code(unique)[code]\nforeign keys: -", await DescribeAsync("mu"));
    }

    private async Task<string> IndexDefinitionAsync(string index) => await RowsAsync(Fixture.ProviderId == "postgres"
        ? "SELECT indexdef FROM pg_indexes WHERE indexname = '" + index + "'"
        : "SELECT sql FROM sqlite_master WHERE type = 'index' AND name = '" + index + "'");

    [SkippableFact(DisplayName = "a composite index keeps a descending column descending, and the ordered read over it returns the rows in that order, on both providers")]
    public async Task A_composite_index_keeps_its_sort_direction()
    {
        Fixture.RequireAvailable();
        await RunAsync(
            "0520_o",
            Table("mo", Col("id", ColumnType.Text, false, true), Col("grp", ColumnType.Text), Col("rank", ColumnType.Int32), Col("note", ColumnType.Text)),
            new CreateIndex("mo_grp_rank", "mo", [new ColumnRef("grp"), new OrderTerm(new ColumnRef("rank"), true)], true));

        var definition = (await IndexDefinitionAsync("mo_grp_rank")).Replace("\"", "", StringComparison.Ordinal);

        Assert.Contains("rank DESC", definition, StringComparison.Ordinal);
        Assert.DoesNotContain("grp DESC", definition, StringComparison.Ordinal);
        Assert.Equal("columns: id:text:notnull:pk1, grp:text:null:pk0, rank:int:null:pk0, note:text:null:pk0\nindexes: mo_grp_rank(unique)[grp,rank]\nforeign keys: -", await DescribeAsync("mo"));
        await ExecAsync("INSERT INTO \"mo\" (\"id\", \"grp\", \"rank\") VALUES ('1', 'a', 1), ('2', 'a', 3), ('3', 'a', 2), ('4', 'b', 9)");
        Assert.Equal("2 ; 3 ; 1", await RowsAsync("SELECT \"id\" FROM \"mo\" WHERE \"grp\" = 'a' ORDER BY \"grp\", \"rank\" DESC"));
        await FailsAsync("INSERT INTO \"mo\" (\"id\", \"grp\", \"rank\") VALUES ('5', 'a', 3)");
    }

    [SkippableFact(DisplayName = "an index term that states where nulls sort is created on PostgreSQL and refused with nothing changed on SQLite, whose index grammar has no such clause")]
    public async Task An_index_term_with_a_null_placement()
    {
        Fixture.RequireAvailable();
        var create = new CreateIndex("mn_rank", "mn", [new OrderTerm(new ColumnRef("rank"), true, NullsPlacement.Last)]);
        await RunAsync("0530_n", Table("mn", Col("id", ColumnType.Text, false, true), Col("rank", ColumnType.Int32)));

        if (Fixture.ProviderId == "postgres")
        {
            await RunAsync("0531_n", create);
            Assert.Contains("DESC NULLS LAST", await IndexDefinitionAsync("mn_rank"), StringComparison.Ordinal);
            return;
        }

        await Assert.ThrowsAnyAsync<Exception>(() => RunAsync("0531_n", create));
        Assert.Equal("", await IndexDefinitionAsync("mn_rank"));
        Assert.Equal("0", await RowsAsync("SELECT count(*) FROM \"schema_migrations\" WHERE \"version\" = '0531_n'"));
    }

    [SkippableFact(DisplayName = "a foreign key added to an existing table with rows enforces from then on, keeps every row, and is refused with nothing changed when an orphan exists")]
    public async Task Add_foreign_key_to_an_existing_table()
    {
        Fixture.RequireAvailable();
        await RunAsync("0600_f", Table("mf_parent", Col("id", ColumnType.Text, false, true)), Table("mf_child", Col("id", ColumnType.Text, false, true), Col("parent_id", ColumnType.Text)));
        await ExecAsync("INSERT INTO \"mf_parent\" (\"id\") VALUES ('p1'), ('p2')");
        await ExecAsync(ManyRows("mf_child", "\"id\", \"parent_id\"", 0, 40, i => $"'c{i}', 'p{i % 2 + 1}'"));
        await ExecAsync("INSERT INTO \"mf_child\" (\"id\", \"parent_id\") VALUES ('orphan', 'ghost')");

        await Assert.ThrowsAnyAsync<Exception>(() => RunAsync("0601_f", new AddForeignKey("fk_mf", "mf_child", Cols("parent_id"), "mf_parent", Cols("id"), ReferentialAction.Cascade)));

        Assert.Equal("columns: id:text:notnull:pk1, parent_id:text:null:pk0\nindexes: -\nforeign keys: -", await DescribeAsync("mf_child"));
        Assert.Equal("41", await RowsAsync("SELECT count(*) FROM \"mf_child\""));
        Assert.Equal("0", await RowsAsync("SELECT count(*) FROM \"schema_migrations\" WHERE \"version\" = '0601_f'"));

        await ExecAsync("DELETE FROM \"mf_child\" WHERE \"id\" = 'orphan'");
        await RunAsync("0602_f", new AddForeignKey("fk_mf", "mf_child", Cols("parent_id"), "mf_parent", Cols("id"), ReferentialAction.Cascade));

        Assert.Equal(
            "columns: id:text:notnull:pk1, parent_id:text:null:pk0\nindexes: -\nforeign keys: fk_mf[parent_id]->mf_parent[id] delete=Cascade update=NoAction",
            await DescribeAsync("mf_child"));
        Assert.Equal("40", await RowsAsync("SELECT count(*) FROM \"mf_child\""));
        await FailsAsync("INSERT INTO \"mf_child\" (\"id\", \"parent_id\") VALUES ('late', 'ghost')");
        await ExecAsync("DELETE FROM \"mf_parent\" WHERE \"id\" = 'p1'");
        Assert.Equal("20", await RowsAsync("SELECT count(*) FROM \"mf_child\""));
    }

    [SkippableFact(DisplayName = "a dropped foreign key stops enforcing, every row stays, and naming a constraint that does not exist is refused")]
    public async Task Drop_foreign_key()
    {
        Fixture.RequireAvailable();
        await RunAsync(
            "0700_g",
            Table("mg_parent", Col("id", ColumnType.Text, false, true)),
            new CreateTable("mg_child", NodeList.Of([Col("id", ColumnType.Text, false, true), Col("parent_id", ColumnType.Text)]), [Link("fk_mg", "parent_id", "mg_parent")]));
        await ExecAsync("INSERT INTO \"mg_parent\" (\"id\") VALUES ('p1')");
        await ExecAsync("INSERT INTO \"mg_child\" (\"id\", \"parent_id\") VALUES ('c1', 'p1')");
        await FailsAsync("INSERT INTO \"mg_child\" (\"id\", \"parent_id\") VALUES ('c2', 'missing')");

        await RunAsync("0701_g", new DropForeignKey("fk_mg", "mg_child"));

        Assert.Equal("columns: id:text:notnull:pk1, parent_id:text:null:pk0\nindexes: -\nforeign keys: -", await DescribeAsync("mg_child"));
        await ExecAsync("INSERT INTO \"mg_child\" (\"id\", \"parent_id\") VALUES ('c2', 'missing')");
        Assert.Equal("c1 ; c2", await RowsAsync("SELECT \"id\" FROM \"mg_child\" ORDER BY \"id\""));
        await Assert.ThrowsAnyAsync<Exception>(() => ApplyAsync(new DropForeignKey("fk_mg", "mg_child")));
    }

    [SkippableFact(DisplayName = "converting a foreign key action changes what a parent delete does, does nothing when the action already matches, and the derived inverse restores the old action")]
    public async Task Convert_foreign_key_action()
    {
        Fixture.RequireAvailable();
        await RunAsync(
            "0800_c",
            Table("mv_parent", Col("id", ColumnType.Text, false, true)),
            new CreateTable("mv_child", NodeList.Of([Col("id", ColumnType.Text, false, true), Col("parent_id", ColumnType.Text)]), [Link("fk_mv", "parent_id", "mv_parent", ReferentialAction.Cascade)]));
        await ExecAsync("INSERT INTO \"mv_parent\" (\"id\") VALUES ('p1'), ('p2')");
        await ExecAsync("INSERT INTO \"mv_child\" (\"id\", \"parent_id\") VALUES ('c1', 'p1'), ('c2', 'p2')");
        var convert = new ConvertForeignKeyAction(
            "mv_child", "fk_mv", Cols("parent_id"), "mv_parent", Cols("id"), ReferentialAction.SetNull, ReferentialAction.Cascade, ReferentialAction.NoAction);

        await RunAsync("0801_c", convert);
        await RunAsync("0802_c", convert);

        Assert.Equal(
            "columns: id:text:notnull:pk1, parent_id:text:null:pk0\nindexes: -\nforeign keys: fk_mv[parent_id]->mv_parent[id] delete=SetNull update=NoAction",
            await DescribeAsync("mv_child"));
        await ExecAsync("DELETE FROM \"mv_parent\" WHERE \"id\" = 'p1'");
        Assert.Equal("c1|~ ; c2|p2", await RowsAsync("SELECT \"id\", \"parent_id\" FROM \"mv_child\" ORDER BY \"id\""));

        await ApplyAsync(MigrationOps.Inverse(convert)!);

        Assert.Contains("delete=Cascade", await DescribeAsync("mv_child"), StringComparison.Ordinal);
        await ExecAsync("DELETE FROM \"mv_parent\" WHERE \"id\" = 'p2'");
        Assert.Equal("c1|~", await RowsAsync("SELECT \"id\", \"parent_id\" FROM \"mv_child\" ORDER BY \"id\""));
    }

    [SkippableFact(DisplayName = "a table with hundreds of child rows survives an added column that SQLite must rebuild the table for: rows, children, indexes and the cascade are all intact")]
    public async Task Rebuilding_a_parent_loses_no_child_rows()
    {
        Fixture.RequireAvailable();
        await RunAsync(
            "0900_h",
            Table("mh_parent", Col("id", ColumnType.Text, false, true), Col("name", ColumnType.Text)),
            new CreateTable("mh_child", NodeList.Of([Col("id", ColumnType.Text, false, true), Col("parent_id", ColumnType.Text, false)]), [Link("fk_mh", "parent_id", "mh_parent", ReferentialAction.Cascade)]),
            new CreateIndex("mh_name", "mh_parent", Cols("name")),
            new CreateIndex("mh_child_parent", "mh_child", Cols("parent_id")));
        await ExecAsync("INSERT INTO \"mh_parent\" (\"id\", \"name\") VALUES ('p1', 'one'), ('p2', 'two'), ('p3', 'three')");
        await ExecAsync(ManyRows("mh_child", "\"id\", \"parent_id\"", 0, 600, i => $"'c{i}', 'p{i % 3 + 1}'"));
        var before = await DescribeAsync("mh_child");

        await RunAsync("0901_h", new AddColumn("mh_parent", Col("stamped_at", ColumnType.Timestamp, false, false, Defaults.Now)));

        Assert.Equal("600", await RowsAsync("SELECT count(*) FROM \"mh_child\""));
        Assert.Equal("p1 ; p2 ; p3", await RowsAsync("SELECT \"id\" FROM \"mh_parent\" ORDER BY \"id\""));
        Assert.Equal("0", await RowsAsync("SELECT count(*) FROM \"mh_parent\" WHERE \"stamped_at\" IS NULL"));
        Assert.Equal(before, await DescribeAsync("mh_child"));
        Assert.Equal(
            "columns: id:text:notnull:pk1, name:text:null:pk0, stamped_at:text:notnull:pk0\nindexes: mh_name(plain)[name]\nforeign keys: -",
            await DescribeAsync("mh_parent"));
        await ExecAsync("DELETE FROM \"mh_parent\" WHERE \"id\" = 'p1'");
        Assert.Equal("400", await RowsAsync("SELECT count(*) FROM \"mh_child\""));
        await FailsAsync("INSERT INTO \"mh_child\" (\"id\", \"parent_id\") VALUES ('late', 'ghost')");
    }

    [SkippableFact(DisplayName = "data operations insert rows with quotes and nulls, delete by value, and a dropped table is gone")]
    public async Task Insert_data_delete_data_and_drop_table()
    {
        Fixture.RequireAvailable();
        await RunAsync(
            "1000_data",
            Table("mx", Col("id", ColumnType.Text, false, true), Col("label", ColumnType.Text)),
            new InsertData("mx", Cols("id", "label"), [new DataRow([new StringLit("1"), new StringLit("it's")]), new DataRow([new StringLit("2"), new NullLit()]), new DataRow([new StringLit("3"), new StringLit("keep")])]),
            new DeleteData("mx", "id", [new StringLit("1"), new StringLit("2")]));

        Assert.Equal("3|keep", await RowsAsync("SELECT \"id\", \"label\" FROM \"mx\""));

        await RunAsync("1001_data", new DropTable("mx"), new DropTable("mx"));

        Assert.Equal("absent", await DescribeAsync("mx"));
    }

    [SkippableFact(DisplayName = "applied versions are recorded once and in order, a re-run skips them, and a version whose operation fails leaves neither its earlier operations nor its history row behind")]
    public async Task Version_history_is_bookkept_per_version()
    {
        Fixture.RequireAvailable();
        MigrationContribution Of(string version, params Node[] operations) => MigrationContribution.Of(version, Reversibility.Reversible, operations);

        await MigrationRunner.RunAsync(Ctx.DataSource, Ctx.Provider, [
            Of("1100_b", new InsertData("mh1", Cols("id"), [new DataRow([new StringLit("b")])])),
            Of("1099_a", Table("mh1", Col("id", ColumnType.Text, false, true))),
        ]);
        await MigrationRunner.RunAsync(Ctx.DataSource, Ctx.Provider, [
            Of("1100_b", new InsertData("mh1", Cols("id"), [new DataRow([new StringLit("b")])])),
            Of("1099_a", Table("mh1", Col("id", ColumnType.Text, false, true))),
        ]);

        Assert.Equal("b", await RowsAsync("SELECT \"id\" FROM \"mh1\""));
        Assert.Equal("1099_a ; 1100_b", await RowsAsync("SELECT \"version\" FROM \"schema_migrations\" WHERE \"version\" IN ('1099_a', '1100_b') ORDER BY \"version\""));

        await Assert.ThrowsAnyAsync<Exception>(() => RunAsync(
            "1101_c",
            Table("mh2", Col("id", ColumnType.Text, false, true)),
            new InsertData("missing_table", Cols("id"), [new DataRow([new StringLit("x")])])));

        Assert.Equal("absent", await DescribeAsync("mh2"));
        Assert.Equal("0", await RowsAsync("SELECT count(*) FROM \"schema_migrations\" WHERE \"version\" = '1101_c'"));
    }

    [SkippableFact(DisplayName = "the soft-delete column is added once with its marker, refused to be removed while soft-deleted rows exist, and removed with its marker once none do")]
    public async Task Soft_delete_column_with_a_marker()
    {
        Fixture.RequireAvailable();
        await RunAsync("1200_s", Table("ms_notes", Col("id", ColumnType.Text, false, true)));
        await ExecAsync("INSERT INTO \"ms_notes\" (\"id\") VALUES ('n1')");

        await ApplyAsync(SoftDeleteColumnDdl.Up("ms_notes", "v1200"));
        await ApplyAsync(SoftDeleteColumnDdl.Up("ms_notes", "v1200"));

        Assert.Equal("columns: id:text:notnull:pk1, deleted_at:text:null:pk0\nindexes: -\nforeign keys: -", await DescribeAsync("ms_notes"));
        await ExecAsync("UPDATE \"ms_notes\" SET \"deleted_at\" = '2026-01-01T00:00:00Z' WHERE \"id\" = 'n1'");
        await Assert.ThrowsAnyAsync<Exception>(() => ApplyAsync(SoftDeleteColumnDdl.Down("ms_notes", "v1200")));
        Assert.Contains("deleted_at", await DescribeAsync("ms_notes"), StringComparison.Ordinal);
        await ExecAsync("UPDATE \"ms_notes\" SET \"deleted_at\" = NULL");

        await ApplyAsync(SoftDeleteColumnDdl.Down("ms_notes", "v1999"));
        Assert.Contains("deleted_at", await DescribeAsync("ms_notes"), StringComparison.Ordinal);
        await ApplyAsync(SoftDeleteColumnDdl.Down("ms_notes", "v1200"));

        Assert.Equal("columns: id:text:notnull:pk1\nindexes: -\nforeign keys: -", await DescribeAsync("ms_notes"));
    }

    [SkippableFact(DisplayName = "a total unique index converts to a live-only partial one, the reverse is refused while duplicates exist and restores the total index once they do not")]
    public async Task Live_only_index_conversion()
    {
        Fixture.RequireAvailable();
        await RunAsync(
            "1300_l",
            Table("ml", Col("id", ColumnType.Text, false, true), Col("tenant", ColumnType.Text), Col("title", ColumnType.Text), Col("deleted_at", ColumnType.Timestamp)),
            new CreateIndex("ml_total", "ml", Cols("tenant", "title"), true));
        await ExecAsync("INSERT INTO \"ml\" (\"id\", \"tenant\", \"title\") VALUES ('1', 't', 'a')");
        Node Convert(bool revert) => new ConvertToLiveOnlyIndex("ml", Cols("tenant", "title"), "deleted_at", "ml_total", "ml_live", false, "v1300", revert);

        await ApplyAsync(Convert(false));
        await ApplyAsync(Convert(false));

        Assert.Equal(
            "columns: id:text:notnull:pk1, tenant:text:null:pk0, title:text:null:pk0, deleted_at:text:null:pk0\nindexes: ml_live(unique,partial)[tenant,title]\nforeign keys: -",
            await DescribeAsync("ml"));
        await ExecAsync("UPDATE \"ml\" SET \"deleted_at\" = '2026-01-01T00:00:00Z'");
        await ExecAsync("INSERT INTO \"ml\" (\"id\", \"tenant\", \"title\") VALUES ('2', 't', 'a')");
        await Assert.ThrowsAnyAsync<Exception>(() => ApplyAsync(Convert(true)));
        Assert.Contains("ml_live", await DescribeAsync("ml"), StringComparison.Ordinal);
        await ExecAsync("DELETE FROM \"ml\" WHERE \"id\" = '1'");

        await ApplyAsync(Convert(true));

        Assert.Equal(
            "columns: id:text:notnull:pk1, tenant:text:null:pk0, title:text:null:pk0, deleted_at:text:null:pk0\nindexes: ml_total(unique)[tenant,title]\nforeign keys: -",
            await DescribeAsync("ml"));
    }

    [SkippableFact(DisplayName = "a migration of reversible operations derives its Down, which restores the schema and forgets the version; one with a lossy operation has none to derive")]
    public async Task Derived_down_restores_the_schema()
    {
        Fixture.RequireAvailable();
        Node[] up =
        [
            Table("mz_parent", Col("id", ColumnType.Text, false, true), Col("v", ColumnType.Text)),
            Table("mz_child", Col("id", ColumnType.Text, false, true), Col("parent_id", ColumnType.Text)),
            new AddForeignKey("fk_mz", "mz_child", Cols("parent_id"), "mz_parent", Cols("id")),
            new CreateIndex("mz_ix", "mz_child", Cols("parent_id")),
            new AddColumn("mz_child", Col("note", ColumnType.Text)),
            new RenameColumn("mz_child", "note", "memo"),
        ];
        var contribution = MigrationContribution.Of("1400_z", Reversibility.Reversible, up);
        await MigrationRunner.RunAsync(Ctx.DataSource, Ctx.Provider, [contribution]);
        Assert.Equal(
            "columns: id:text:notnull:pk1, parent_id:text:null:pk0, memo:text:null:pk0\nindexes: mz_ix(plain)[parent_id]\nforeign keys: fk_mz[parent_id]->mz_parent[id] delete=NoAction update=NoAction",
            await DescribeAsync("mz_child"));
        Assert.Equal("1", await RowsAsync("SELECT count(*) FROM \"schema_migrations\" WHERE \"version\" = '1400_z'"));

        await MigrationRunner.RevertAsync(Ctx.DataSource, Ctx.Provider, contribution);

        Assert.Equal("absent", await DescribeAsync("mz_child"));
        Assert.Equal("absent", await DescribeAsync("mz_parent"));
        Assert.Equal("0", await RowsAsync("SELECT count(*) FROM \"schema_migrations\" WHERE \"version\" = '1400_z'"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => MigrationRunner.RevertAsync(Ctx.DataSource, Ctx.Provider, contribution));

        var lossy = MigrationContribution.Of("1401_z", Reversibility.Irreversible, [new DropColumn("mz_x", "c")]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => MigrationRunner.RevertAsync(Ctx.DataSource, Ctx.Provider, lossy));
    }

    [SkippableFact(DisplayName = "the operation classes a provider runs are the classes the classifier reports, so Up and the derived Down agree on both providers")]
    public void Classes_are_the_same_on_both_providers()
    {
        Fixture.RequireAvailable();

        Assert.Equal(OpClass.Reversible, MigrationOps.ClassOf(new AddForeignKey("f", "t", Cols("a"), "o", Cols("id"))));
        Assert.Equal(OpClass.Lossy, MigrationOps.ClassOf(new DropForeignKey("f", "t")));
        Assert.Null(MigrationOps.DeriveDown([new DropColumn("t", "c")]));
        Assert.Equal(new Node[] { new DropForeignKey("f", "t") }, MigrationOps.DeriveDown([new AddForeignKey("f", "t", Cols("a"), "o", Cols("id"))]));
    }
}

public sealed class SqliteMigrationTests(SqliteFixture fixture) : MigrationSuite<SqliteFixture>(fixture);

public sealed class PostgresMigrationTests(PostgresFixture fixture) : MigrationSuite<PostgresFixture>(fixture);
