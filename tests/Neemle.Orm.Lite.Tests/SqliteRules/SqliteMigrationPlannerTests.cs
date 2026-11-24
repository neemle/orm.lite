using System.Data.Common;
using System.Globalization;
using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sqlite;
using Neemle.Orm.Lite.Tests.Behaviour;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SqliteRules;

public sealed class SqliteMigrationPlannerTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private OrmContext Ctx => fixture.Context;

    private static ColumnDef Col(string name, ColumnType type, bool nullable = true, bool key = false, Node? fallback = null) =>
        new(name, new TypeRef(type), nullable, key, fallback);

    private static NodeList Cols(params string[] names) => NodeList.Of(names.Select(n => (Node)new ColumnRef(n)));

    private Task RunAsync(string version, params Node[] operations) =>
        MigrationRunner.RunAsync(Ctx.DataSource, Ctx.Provider, [MigrationContribution.Of(version, Reversibility.Reversible, operations)]);

    private Task ApplyAsync(params Node[] operations) => MigrationRunner.ApplyAsync(Ctx.DataSource, Ctx.Provider, operations);

    private async Task<string> RowsAsync(string statement)
    {
        var rows = await fixture.RowsAsync(statement);
        return string.Join(" ; ", rows.Select(r => string.Join("|", r.Select(v => v is null ? "~" : Convert.ToString(v, CultureInfo.InvariantCulture)))));
    }

    private Task<string> SchemaAsync(string where = "1 = 1") =>
        RowsAsync("SELECT \"type\", \"name\", \"tbl_name\", \"sql\" FROM \"sqlite_schema\" WHERE " + where + " ORDER BY \"name\"");

    private static string ManyChildren(string table, int count, int parents) =>
        "INSERT INTO \"" + table + "\" (\"id\", \"parent_id\") VALUES "
        + string.Join(", ", Enumerable.Range(0, count).Select(i => $"('c{i}', 'p{i % parents + 1}')"));

    private async Task SeedParentAndChildrenAsync(string stem, int children)
    {
        await RunAsync(
            "0001_" + stem,
            new CreateTable(stem + "_parent", NodeList.Of([Col("id", ColumnType.Text, false, true), Col("name", ColumnType.Text), Col("gone", ColumnType.Text)])),
            new CreateTable(
                stem + "_child",
                NodeList.Of([Col("id", ColumnType.Text, false, true), Col("parent_id", ColumnType.Text, false)]),
                [new ForeignKeyDef("fk_" + stem, Cols("parent_id"), stem + "_parent", Cols("id"), ReferentialAction.Cascade)]),
            new CreateIndex(stem + "_live", stem + "_parent", Cols("name"), true, new Where([new IsNull(new ColumnRef("gone"))])),
            new CreateIndex(stem + "_child_parent", stem + "_child", Cols("parent_id")));
        await fixture.ExecuteAsync("INSERT INTO \"" + stem + "_parent\" (\"id\", \"name\") VALUES ('p1', 'one'), ('p2', 'two'), ('p3', 'three')");
        await fixture.ExecuteAsync(ManyChildren(stem + "_child", children, 3));
    }

    [Fact(DisplayName = "rebuilding a parent table keeps every row, 600 child rows, the partial index, a trigger and a view byte for byte, the cascade and the foreign key, and leaves enforcement on")]
    public async Task A_rebuild_loses_nothing()
    {
        await SeedParentAndChildrenAsync("sr", 600);
        await fixture.ExecuteAsync("CREATE TABLE sr_audit (id TEXT)");
        await fixture.ExecuteAsync("CREATE TRIGGER sr_parent_log AFTER UPDATE ON sr_parent BEGIN INSERT INTO sr_audit (id) VALUES (new.id); END");
        await fixture.ExecuteAsync("CREATE VIEW sr_view AS SELECT p.id AS id, count(c.id) AS n FROM sr_parent p LEFT JOIN sr_child c ON c.parent_id = p.id GROUP BY p.id");
        var attached = "\"name\" IN ('sr_live', 'sr_parent_log', 'sr_view', 'sr_child', 'sr_child_parent', 'sr_audit')";
        var before = await SchemaAsync(attached);

        await RunAsync("0002_sr", new AddColumn("sr_parent", Col("stamped_at", ColumnType.Timestamp, false, false, Defaults.Now)));

        Assert.Equal(before, await SchemaAsync(attached));
        Assert.Equal("600", await RowsAsync("SELECT count(*) FROM sr_child"));
        Assert.Equal("p1|one|~ ; p2|two|~ ; p3|three|~", await RowsAsync("SELECT \"id\", \"name\", \"gone\" FROM sr_parent ORDER BY \"id\""));
        Assert.Equal("0", await RowsAsync("SELECT count(*) FROM sr_parent WHERE stamped_at IS NULL"));
        Assert.Equal("p1|200 ; p2|200 ; p3|200", await RowsAsync("SELECT * FROM sr_view ORDER BY id"));
        await fixture.ExecuteAsync("UPDATE sr_parent SET name = 'uno' WHERE id = 'p1'");
        Assert.Equal("p1", await RowsAsync("SELECT id FROM sr_audit"));
        await Assert.ThrowsAnyAsync<DbException>(() => fixture.ExecuteAsync("INSERT INTO sr_parent (id, name) VALUES ('p9', 'two')"));
        await Assert.ThrowsAnyAsync<DbException>(() => fixture.ExecuteAsync("INSERT INTO sr_child (id, parent_id) VALUES ('late', 'ghost')"));
        Assert.Equal("ok", await RowsAsync("PRAGMA integrity_check"));
        Assert.Equal("", await RowsAsync("PRAGMA foreign_key_check"));
        Assert.Equal("1", await RowsAsync("PRAGMA foreign_keys"));
        Assert.Equal("", await SchemaAsync("\"name\" LIKE '%__orm_rebuild'"));
        await fixture.ExecuteAsync("DELETE FROM sr_parent WHERE id = 'p2'");
        Assert.Equal("400", await RowsAsync("SELECT count(*) FROM sr_child"));
    }

    [Fact(DisplayName = "a rebuild whose copy fails leaves the database byte for byte as it was: schema, rows, children, no history row, enforcement back on")]
    public async Task A_failing_rebuild_changes_nothing()
    {
        await SeedParentAndChildrenAsync("sf", 90);
        var before = await SchemaAsync();

        await Assert.ThrowsAnyAsync<DbException>(() => RunAsync("0002_sf", new AddColumn("sf_parent", Col("required", ColumnType.Text, false))));

        Assert.Equal(before, await SchemaAsync());
        Assert.Equal("3", await RowsAsync("SELECT count(*) FROM sf_parent"));
        Assert.Equal("90", await RowsAsync("SELECT count(*) FROM sf_child"));
        Assert.Equal("0", await RowsAsync("SELECT count(*) FROM schema_migrations WHERE version = '0002_sf'"));
        Assert.Equal("1", await RowsAsync("PRAGMA foreign_keys"));
    }

    [Fact(DisplayName = "a rebuild that would leave a row violating a foreign key is refused at the end of the version and rolls the whole version back")]
    public async Task A_foreign_key_violation_rolls_the_version_back()
    {
        await RunAsync("0001_sv", new CreateTable("sv_parent", [Col("id", ColumnType.Text, false, true)]), new CreateTable("sv_child", [Col("id", ColumnType.Text, false, true), Col("parent_id", ColumnType.Text)]));
        await fixture.ExecuteAsync("INSERT INTO sv_child (id, parent_id) VALUES ('c1', 'ghost')");
        var before = await SchemaAsync();

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAsync("0002_sv", new CreateTable("sv_other", [Col("id", ColumnType.Text, false, true)]), new AddForeignKey("fk_sv", "sv_child", Cols("parent_id"), "sv_parent", Cols("id"))));

        Assert.Contains("sv_child", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(before, await SchemaAsync());
        Assert.Equal("1", await RowsAsync("PRAGMA foreign_keys"));
    }

    [Fact(DisplayName = "a table that carries a row-history trigger is refused by name before any statement, and with a regenerator installed the trigger is regenerated for the new column set")]
    public async Task History_triggers_are_a_hook()
    {
        await RunAsync("0001_sh", new CreateTable("sh_t", [Col("id", ColumnType.Text, false, true), Col("name", ColumnType.Text)]));
        await fixture.ExecuteAsync("CREATE TRIGGER orm_history_sh_t AFTER UPDATE ON sh_t BEGIN SELECT 1; END");
        var before = await SchemaAsync();
        var add = new AddColumn("sh_t", Col("stamped_at", ColumnType.Timestamp, false, false, Defaults.Now));

        var refusal = await Assert.ThrowsAsync<NotSupportedException>(() => RunAsync("0002_sh", add));

        Assert.Contains("orm_history_sh_t", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(before, await SchemaAsync());

        var regenerator = new RecordingRegenerator();
        var hooked = new OrmContext(Ctx.DataSource, new SqliteProvider(SqliteProvider.DefaultBusyTimeout, regenerator));
        await MigrationRunner.RunAsync(hooked.DataSource, hooked.Provider, [MigrationContribution.Of("0003_sh", Reversibility.Reversible, [add])]);

        Assert.Equal(("sh_t", "id,name,stamped_at", "orm_history_sh_t"), (regenerator.Table, string.Join(",", regenerator.Columns), string.Join(",", regenerator.Triggers)));
        Assert.Equal("orm_history_sh_t|CREATE TRIGGER orm_history_sh_t AFTER UPDATE ON sh_t BEGIN SELECT 2; END", await RowsAsync("SELECT name, sql FROM sqlite_schema WHERE type = 'trigger' AND tbl_name = 'sh_t'"));
    }

    private sealed class RecordingRegenerator : ISqliteTriggerRegenerator
    {
        internal string Table { get; private set; } = "";

        internal IReadOnlyList<string> Columns { get; private set; } = [];

        internal IReadOnlyList<string> Triggers { get; private set; } = [];

        public bool Owns(string triggerName) => triggerName.StartsWith("orm_history_", StringComparison.Ordinal);

        public IReadOnlyList<Node> Recreate(string table, IReadOnlyList<string> columns, IReadOnlyList<string> triggers, string? marker)
        {
            (Table, Columns, Triggers) = (table, columns, triggers);
            return [new SqliteStored("CREATE TRIGGER orm_history_sh_t AFTER UPDATE ON sh_t BEGIN SELECT 2; END")];
        }
    }

    [Fact(DisplayName = "a rebuild is planned only by a unit that switched enforcement off, so a naive rebuild cannot run and delete the children")]
    public async Task A_rebuild_needs_enforcement_off()
    {
        await SeedParentAndChildrenAsync("sg", 30);
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var unit = new SqliteMigrationUnit((SqliteProvider)Ctx.Provider, connection, false);

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            unit.PlanAsync(new AddColumn("sg_parent", Col("stamped_at", ColumnType.Timestamp, false, false, Defaults.Now)), transaction, CancellationToken.None));

        Assert.Contains("foreign key enforcement off", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a unit that switches enforcement off for a rebuild switches it back on when it ends, on the same connection")]
    public async Task The_unit_restores_enforcement()
    {
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        async Task<string> Pragma()
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys";
            return Convert.ToString(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture)!;
        }

        var unit = new SqliteMigrationUnit((SqliteProvider)Ctx.Provider, connection, true);
        await unit.PrepareAsync(CancellationToken.None);
        var during = await Pragma();
        await unit.DisposeAsync();

        Assert.Equal(("0", "1"), (during, await Pragma()));
    }

    [Fact(DisplayName = "every refusal is raised before any statement of the version runs: a column type SQLite cannot hold, a node it cannot run, and raw text scoped to another provider")]
    public async Task Refusals_leave_nothing_behind()
    {
        var cases = new (string Version, Node Refused, Type Expected)[]
        {
            ("0001_x_dec", new CreateTable("rx_json", [new ColumnDef("p", new TypeRef(ColumnType.Decimal, 29, 4))]), typeof(NotSupportedException)),
            ("0002_x_decimal", new CreateTable("rx_dec", [new ColumnDef("p", new TypeRef(ColumnType.Decimal, 4, 5))]), typeof(NotSupportedException)),
            ("0004_x_table", new HistoryTableNode("t", "id", "deleted_at", "", [], "0123456789abcdef"), typeof(CapabilityNotSupportedException)),
            ("0005_x_lock", new SessionLock(SessionLockOp.Wait, new IntLit(1)), typeof(CapabilityNotSupportedException)),
            ("0006_x_raw", new RawSql("postgres", "SELECT 1"), typeof(InvalidOperationException)),
        };

        foreach (var (version, refused, expected) in cases)
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() => RunAsync(version, new CreateTable("rx_first", [Col("id", ColumnType.Text, false, true)]), refused));

            Assert.IsType(expected, error);
            Assert.Equal("", await SchemaAsync("\"name\" = 'rx_first'"));
            Assert.Equal("0", await RowsAsync("SELECT count(*) FROM schema_migrations WHERE version = '" + version + "'"));
        }
    }

    [Fact(DisplayName = "a column that belongs to the primary key, a unique constraint or a foreign key is refused by name, and the table is untouched")]
    public async Task A_constraint_bound_column_is_not_dropped_implicitly()
    {
        await fixture.ExecuteAsync("CREATE TABLE sc_parent (id TEXT PRIMARY KEY)");
        await fixture.ExecuteAsync("CREATE TABLE sc_t (id TEXT PRIMARY KEY, a TEXT, b TEXT, c TEXT, UNIQUE (b), CONSTRAINT fk_sc FOREIGN KEY (a) REFERENCES sc_parent (id))");
        var before = await SchemaAsync();

        var key = await Assert.ThrowsAsync<NotSupportedException>(() => ApplyAsync(new DropColumn("sc_t", "id")));
        var unique = await Assert.ThrowsAsync<NotSupportedException>(() => ApplyAsync(new DropColumn("sc_t", "b")));
        var reference = await Assert.ThrowsAsync<NotSupportedException>(() => ApplyAsync(new DropColumn("sc_t", "a")));
        var referenced = await Assert.ThrowsAsync<NotSupportedException>(() => ApplyAsync(new DropColumn("sc_parent", "id")));

        Assert.Contains("primary key", key.Message, StringComparison.Ordinal);
        Assert.Contains("unique constraint", unique.Message, StringComparison.Ordinal);
        Assert.Contains("foreign key", reference.Message, StringComparison.Ordinal);
        Assert.Contains("primary key", referenced.Message, StringComparison.Ordinal);
        Assert.Equal(before, await SchemaAsync());
        await ApplyAsync(new DropColumn("sc_t", "c"));
        Assert.Equal("id,a,b", await RowsAsync("SELECT group_concat(name) FROM pragma_table_info('sc_t')"));
    }

    [Fact(DisplayName = "dropping a column removes the indexes that name it in a key, an expression or a predicate, and keeps the rest")]
    public async Task Dropping_a_column_removes_every_index_that_names_it()
    {
        await RunAsync(
            "0001_sd",
            new CreateTable("sd_t", [Col("id", ColumnType.Text, false, true), Col("a", ColumnType.Text), Col("b", ColumnType.Text), Col("c", ColumnType.Text)]),
            new CreateIndex("sd_key", "sd_t", Cols("a", "b")),
            new CreateIndex("sd_expr", "sd_t", [new Call(Fn.Lower, [new ColumnRef("b")])]),
            new CreateIndex("sd_pred", "sd_t", Cols("a"), false, new Where([new IsNotNull(new ColumnRef("b"))])),
            new CreateIndex("sd_keep", "sd_t", Cols("a", "c")));

        await RunAsync("0002_sd", new DropColumn("sd_t", "b"));

        Assert.Equal("sd_keep", await RowsAsync("SELECT name FROM sqlite_schema WHERE type = 'index' AND tbl_name = 'sd_t' AND sql IS NOT NULL"));
    }

    [Fact(DisplayName = "the emulated markers live in one table, are written with the object they mark, and go when the column, the index or the table does")]
    public async Task Markers_follow_their_objects()
    {
        await RunAsync("0001_mk", new CreateTable("mk_t", [Col("id", ColumnType.Text, false, true), Col("tenant", ColumnType.Text), Col("title", ColumnType.Text)]), new CreateIndex("mk_total", "mk_t", Cols("tenant", "title"), true));
        await ApplyAsync(SoftDeleteColumnDdl.Up("mk_t", "v1"));
        await ApplyAsync(new ConvertToLiveOnlyIndex("mk_t", Cols("tenant", "title"), "deleted_at", "mk_total", "mk_live", false, "v2", false));

        Assert.Equal("column:mk_t.deleted_at|v1 ; index:mk_live|v2", await RowsAsync("SELECT object_key, marker FROM orm_object_markers WHERE object_key LIKE '%mk_%' ORDER BY object_key"));

        await ApplyAsync(new DropIndex("mk_live"));
        Assert.Equal("column:mk_t.deleted_at|v1", await RowsAsync("SELECT object_key, marker FROM orm_object_markers WHERE object_key LIKE '%mk_%' ORDER BY object_key"));

        await ApplyAsync(new DropTable("mk_t"));
        Assert.Equal("", await RowsAsync("SELECT object_key, marker FROM orm_object_markers WHERE object_key LIKE '%mk_%'"));
    }

    [Fact(DisplayName = "dropping the soft-delete column by itself forgets its marker, so a later column of that name is not mistaken for the marked one")]
    public async Task Dropping_a_marked_column_forgets_the_marker()
    {
        await RunAsync("0001_mc", new CreateTable("mc_t", [Col("id", ColumnType.Text, false, true)]));
        await ApplyAsync(SoftDeleteColumnDdl.Up("mc_t", "v1"));
        Assert.Equal("1", await RowsAsync("SELECT count(*) FROM orm_object_markers WHERE object_key = 'column:mc_t.deleted_at'"));

        await ApplyAsync(new DropColumn("mc_t", "deleted_at"));

        Assert.Equal("0", await RowsAsync("SELECT count(*) FROM orm_object_markers WHERE object_key = 'column:mc_t.deleted_at'"));
    }

    [Fact(DisplayName = "reversing a live-only conversion while a duplicate key exists is refused by the planner with the duplicate named, and neither index changes")]
    public async Task A_duplicate_key_refuses_the_reverse_conversion()
    {
        await RunAsync(
            "0001_ld",
            new CreateTable("ld_t", [Col("id", ColumnType.Text, false, true), Col("k", ColumnType.Text), Col("deleted_at", ColumnType.Timestamp)]),
            new CreateIndex("ld_total", "ld_t", Cols("k"), true));
        Node Convert(bool revert) => new ConvertToLiveOnlyIndex("ld_t", Cols("k"), "deleted_at", "ld_total", "ld_live", false, "v1", revert);
        await ApplyAsync(Convert(false));
        await fixture.ExecuteAsync("INSERT INTO ld_t (id, k, deleted_at) VALUES ('1', 'a', '2026-01-01T00:00:00Z')");
        await fixture.ExecuteAsync("INSERT INTO ld_t (id, k) VALUES ('2', 'a')");
        var before = await SchemaAsync("tbl_name = 'ld_t'");

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => ApplyAsync(Convert(true)));

        Assert.Contains("nothing was deleted", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(before, await SchemaAsync("tbl_name = 'ld_t'"));
    }

    [Fact(DisplayName = "a total index owned by a constraint is refused by name instead of silently left in place")]
    public async Task A_constraint_owned_index_is_not_converted()
    {
        await fixture.ExecuteAsync("CREATE TABLE mo_t (id TEXT PRIMARY KEY, a TEXT, deleted_at TEXT, CONSTRAINT mo_total UNIQUE (a))");
        var before = await SchemaAsync();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            ApplyAsync(new ConvertToLiveOnlyIndex("mo_t", Cols("a"), "deleted_at", "sqlite_autoindex_mo_t_1", "mo_live", false, "v1", false)));

        Assert.Equal(before, await SchemaAsync());
    }

    [Fact(DisplayName = "a reconcile set refuses a live-only index whose columns already carry a total unique index or constraint, naming it, creates the others and leaves the schema as it was")]
    public async Task A_reconcile_refuses_a_collision_with_a_total_unique_key()
    {
        await fixture.ExecuteAsync("CREATE TABLE rc_t (id TEXT PRIMARY KEY, a TEXT, b TEXT, c TEXT, deleted_at TEXT, CONSTRAINT rc_total UNIQUE (a))");
        await fixture.ExecuteAsync("CREATE UNIQUE INDEX rc_other ON rc_t (b)");
        var before = await SchemaAsync();
        static ReconcileIndex Live(string index, string column) => new(
            new CreateIndex(index, "rc_t", NodeList.Of([new ColumnRef(column)]), true, new Where([new IsNull(new ColumnRef("deleted_at"))])),
            NodeList.Of([new ColumnRef(column), new ColumnRef("deleted_at")]),
            NodeList.Of([new ColumnRef(column)]));

        var constraint = await Assert.ThrowsAsync<InvalidOperationException>(() => ApplyAsync(new ReconcileIndexes([Live("rc_live_a", "a")])));
        var index = await Assert.ThrowsAsync<InvalidOperationException>(() => ApplyAsync(new ReconcileIndexes([Live("rc_live_b", "b")])));

        Assert.Contains("rc_live_a", constraint.Message, StringComparison.Ordinal);
        Assert.Contains("constraint", constraint.Message, StringComparison.Ordinal);
        Assert.Contains("rc_other", index.Message, StringComparison.Ordinal);
        Assert.Equal(before, await SchemaAsync());

        await ApplyAsync(new ReconcileIndexes([Live("rc_live_c", "c")]));

        Assert.Equal("1", await RowsAsync("SELECT count(*) FROM sqlite_schema WHERE name = 'rc_live_c'"));
    }

    [Fact(DisplayName = "two runners started together apply each version exactly once and record it once")]
    public async Task Concurrent_runners_apply_once()
    {
        MigrationContribution[] Contributions() =>
        [
            MigrationContribution.Of("0001_cc", Reversibility.Reversible, [new CreateTable("cc_t", [Col("id", ColumnType.Text, false, true)])]),
            MigrationContribution.Of("0002_cc", Reversibility.Reversible, [new InsertData("cc_t", Cols("id"), [new DataRow([new StringLit("only")])])]),
        ];

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => MigrationRunner.RunAsync(Ctx.DataSource, Ctx.Provider, Contributions()))));

        Assert.Equal("only", await RowsAsync("SELECT id FROM cc_t"));
        Assert.Equal("2", await RowsAsync("SELECT count(*) FROM schema_migrations WHERE version LIKE '%_cc'"));
    }

    [Fact(DisplayName = "the catalog reads a composite foreign key with its name, a quoted name containing a comma, an implicit parent key and a lowered index term")]
    public async Task The_catalog_reads_what_it_is_given()
    {
        await fixture.ExecuteAsync("CREATE TABLE ct_parent (a TEXT, b TEXT, PRIMARY KEY (a, b))");
        await fixture.ExecuteAsync("CREATE TABLE ct_child (id TEXT PRIMARY KEY, x TEXT, y TEXT, \"we,ird\" TEXT, CHECK (length(x) > 0 AND length(y) > 0), CONSTRAINT \"fk, one\" FOREIGN KEY (x, y) REFERENCES ct_parent ON DELETE SET NULL ON UPDATE CASCADE)");
        await fixture.ExecuteAsync("CREATE INDEX ct_lower ON ct_child (lower(x), y)");
        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        var catalog = Ctx.Provider.CreateCatalog(connection);

        var keys = await catalog.ForeignKeysAsync("ct_child");
        var indexes = await catalog.IndexesAsync("ct_child");
        var columns = await catalog.ColumnsAsync("ct_child");

        var key = Assert.Single(keys);
        Assert.Equal("fk, one", key.Name);
        Assert.Equal(["x", "y"], key.Columns);
        Assert.Equal(["a", "b"], key.ReferencedColumns);
        Assert.Equal((ReferentialAction.SetNull, ReferentialAction.Cascade), (key.OnDelete, key.OnUpdate));
        Assert.Equal(["<expression>", "y"], Assert.Single(indexes).Terms);
        Assert.Equal(["id", "x", "y", "we,ird"], columns.Select(c => c.Name));
        Assert.False(await catalog.TableExistsAsync("nowhere"));
        Assert.True(await catalog.TableExistsAsync("CT_CHILD"));
    }

    [Fact(DisplayName = "a rebuild keeps a column check, a collation, a table option and an unrelated trigger that mentions the table, which the text replay carries as it was")]
    public async Task A_rebuild_replays_what_it_does_not_edit()
    {
        await fixture.ExecuteAsync("CREATE TABLE sk_log (id TEXT)");
        await fixture.ExecuteAsync("CREATE TABLE sk_t (id TEXT PRIMARY KEY, n INTEGER CHECK (n >= 0), label TEXT COLLATE NOCASE, UNIQUE (label))");
        await fixture.ExecuteAsync("CREATE TRIGGER sk_other AFTER INSERT ON sk_log BEGIN UPDATE sk_t SET n = n + 1; END");
        await fixture.ExecuteAsync("INSERT INTO sk_t (id, n, label) VALUES ('1', 5, 'Abc')");

        await ApplyAsync(new AddColumn("sk_t", Col("stamped_at", ColumnType.Timestamp, false, false, Defaults.Now)));

        await Assert.ThrowsAnyAsync<DbException>(() => fixture.ExecuteAsync("INSERT INTO sk_t (id, n, label) VALUES ('2', -1, 'x')"));
        await Assert.ThrowsAnyAsync<DbException>(() => fixture.ExecuteAsync("INSERT INTO sk_t (id, n, label) VALUES ('3', 1, 'ABC')"));
        await fixture.ExecuteAsync("INSERT INTO sk_log (id) VALUES ('go')");
        Assert.Equal("6", await RowsAsync("SELECT n FROM sk_t"));
        Assert.Equal("sk_other", await RowsAsync("SELECT name FROM sqlite_schema WHERE type = 'trigger' AND tbl_name = 'sk_log'"));
    }
}
