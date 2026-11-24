using Microsoft.Data.Sqlite;
using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Sqlite;
using Neemle.Orm.Lite.Tests.Behaviour;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SqliteRules;

public sealed class SqliteHistoryTriggerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static async Task<TempSqlite> OpenAsync(Action<OrmLiteOptions>? configure = null)
    {
        var temp = await TempSqlite.OpenAsync(configure, schema: false);
        HistoryWorld.Register();
        await ApplyAsync(temp, [.. HistoryWorld.Schema()]);
        await ApplyAsync(temp, RowHistoryDdl.Install(HistoryWorld.Tables));
        return temp;
    }

    private static Task ApplyAsync(TempSqlite temp, params Node[] operations) =>
        MigrationRunner.ApplyAsync(temp.Context.DataSource, temp.Context.Provider, operations);

    private static Task RunAsync(TempSqlite temp, string version, params Node[] operations) =>
        MigrationRunner.RunAsync(temp.Context.DataSource, temp.Context.Provider, [MigrationContribution.Of(version, Reversibility.Reversible, operations)]);

    private static ColumnDef Col(string name, ColumnType type, bool nullable = true, Node? fallback = null) =>
        new(name, new TypeRef(type), nullable, false, fallback);

    private static async Task<Guid> InsertTaskAsync(TempSqlite temp, string name = "a")
    {
        var id = Guid.NewGuid();
        await new Query<HistTask>(temp.Context, HistTask.Map).InsertManyAsync([
            new Row<HistTask>().With(t => t.Id, id).With(t => t.TenantId, Tenant).With(t => t.Name, name)
                .With(t => t.Rank, 1).With(t => t.Status, "open"),
        ]);
        return id;
    }

    private static Task<IReadOnlyList<RowHistoryVersion>> VersionsAsync(TempSqlite temp, Guid id) =>
        RowHistoryRead.VersionsAsync(temp.Context, HistTask.Table, id, Tenant);

    // After the key column is renamed in the database the registered key column is still "id", so a tenant-scoped
    // read (it checks the live row by that name) cannot run; the stored history is read through the unscoped statement.
    private static async Task<string[]> NewestChangedKeysUnscopedAsync(TempSqlite temp, Guid id)
    {
        var statement = HistoryStatements.Compile(
            temp.Context.Provider, HistoryStatements.Versions(HistTask.Table, null, null, HistTask.Table, id, null));
        await using var cmd = temp.Context.DataSource.CreateCommand(statement.Text);
        HistoryStatements.Bind(cmd, statement);
        await using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? HistoryReads.Keys(reader, 4) : [];
    }

    private static string Statement(Node node) => StatementCompiler.Compile(node, SqliteProvider.Instance, StatementKind.Ddl).Text;

    private static string Quoted(Guid id) => "'" + id.ToString("D") + "'";

    private static SqliteConnection Foreign(TempSqlite temp, bool pooled = true) =>
        new(SqliteProviderFactory.ConnectionStringOf(new OrmLiteOptions { Provider = "sqlite", Path = temp.Path_, Pooling = pooled ? null : false }));

    private async Task<string> TriggersAsync(TempSqlite temp, string table) =>
        Convert.ToString(await temp.ScalarAsync($"SELECT group_concat(name, ',') FROM (SELECT name FROM sqlite_schema WHERE type = 'trigger' AND tbl_name = '{table}' ORDER BY name)"), System.Globalization.CultureInfo.InvariantCulture) ?? "";

    [Fact(DisplayName = "the history store and the two triggers of a table are written from nodes, with the changed columns as a JSON object and the actor and transaction read from the connection functions")]
    public void The_history_statements_are_pinned()
    {
        var spec = new SqliteHistorySpec("id", "deleted_at", null, ["updated_at"]);

        var store = Statement(new SqliteHistoryStore());
        var triggers = spec.Triggers("t", ["id", "name", "updated_at", "deleted_at"]).Select(Statement).ToList();

        Assert.Equal(Expected.Store, store);
        Assert.Equal([Expected.Update, Expected.Delete], triggers);
    }

    [Theory(DisplayName = "a second connection that has not registered the functions, pooled with the library or not, can read a tracked table and insert or delete rows, but an update is refused and leaves the row and its history as they were")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_foreign_writer_without_the_functions_fails_closed(bool pooled)
    {
        await using var temp = await OpenAsync();
        var id = await InsertTaskAsync(temp);
        await using var foreign = Foreign(temp, pooled);
        await foreign.OpenAsync();
        async Task<object?> Scalar(string sql) { await using var c = foreign.CreateCommand(); c.CommandText = sql; return await c.ExecuteScalarAsync(); }

        Assert.Equal(1L, await Scalar("SELECT count(*) FROM hist_tasks"));
        var refusal = await Assert.ThrowsAsync<SqliteException>(() => Scalar($"UPDATE hist_tasks SET name = 'x' WHERE id = {Quoted(id)}"));

        Assert.Contains("no such function", refusal.Message, StringComparison.Ordinal);
        Assert.Equal("a", Convert.ToString(await Scalar($"SELECT name FROM hist_tasks WHERE id = {Quoted(id)}")));
        Assert.Empty(await VersionsAsync(temp, id));
        await Scalar($"INSERT INTO hist_tasks (id, tenant_id, name, rank) VALUES ('{Guid.NewGuid():D}', '{Guid.NewGuid():D}', 'z', 1)");
        await Scalar($"DELETE FROM hist_tasks WHERE id = {Quoted(id)}");
        Assert.Equal(1L, await Scalar("SELECT count(*) FROM hist_tasks"));
    }

    [Fact(DisplayName = "a library connection records history with trusted_schema off because its functions are registered innocuous, while a function registered the ordinary way is refused there")]
    public async Task The_functions_are_innocuous()
    {
        await using var temp = await OpenAsync();
        var id = await InsertTaskAsync(temp);
        await using (var hardened = await temp.Context.DataSource.OpenConnectionAsync())
        {
            await using var pragma = hardened.CreateCommand();
            pragma.CommandText = "PRAGMA trusted_schema = OFF";
            await pragma.ExecuteNonQueryAsync();
            await using var update = hardened.CreateCommand();
            update.CommandText = $"UPDATE hist_tasks SET name = 'b' WHERE id = {Quoted(id)}";
            await update.ExecuteNonQueryAsync();
        }
        Assert.Equal(["name"], Assert.Single(await VersionsAsync(temp, id)).ChangedKeys);

        await using var ordinary = Foreign(temp);
        await ordinary.OpenAsync();
        ordinary.CreateFunction(SqliteFunctions.Actor, () => (string?)null);
        ordinary.CreateFunction(SqliteFunctions.Transaction, () => 1L);
        ordinary.CreateFunction(SqliteFunctions.TransactionTime, () => "2026-01-01T00:00:00.0000000Z");
        await using var off = ordinary.CreateCommand();
        off.CommandText = "PRAGMA trusted_schema = OFF";
        await off.ExecuteNonQueryAsync();
        await using var refused = ordinary.CreateCommand();
        refused.CommandText = $"UPDATE hist_tasks SET name = 'c' WHERE id = {Quoted(id)}";
        var error = await Assert.ThrowsAsync<SqliteException>(() => refused.ExecuteNonQueryAsync());
        Assert.Contains("unsafe use of", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "every rent of a pooled connection registers the functions again, so each one records its update")]
    public async Task Pooled_rents_record_history()
    {
        await using var temp = await OpenAsync(o => o.Pooling = true);
        var id = await InsertTaskAsync(temp);

        for (var rent = 0; rent < 4; rent++)
            await new Query<HistTask>(temp.Context, HistTask.Map).Where(t => t.Id == id).Set(t => t.Name, "n" + rent).UpdateAsync();

        Assert.Equal(4, (await VersionsAsync(temp, id)).Count);
    }

    [Fact(DisplayName = "a REPLACE that overwrites a tracked row fires the delete trigger, so the history of the replaced row is purged instead of inherited by its successor")]
    public async Task A_replace_purges_the_history_of_the_replaced_row()
    {
        await using var temp = await OpenAsync();
        var id = await InsertTaskAsync(temp);
        await new Query<HistTask>(temp.Context, HistTask.Map).Where(t => t.Id == id).Set(t => t.Name, "b").UpdateAsync();
        Assert.Single(await VersionsAsync(temp, id));
        Assert.Equal(1L, await temp.ScalarAsync($"SELECT count(*) FROM row_history WHERE row_id = {Quoted(id)}"));

        await temp.ExecuteAsync($"INSERT OR REPLACE INTO hist_tasks (id, tenant_id, name, rank) VALUES ({Quoted(id)}, '{Guid.NewGuid():D}', 'r', 1)");

        Assert.Equal(0L, await temp.ScalarAsync($"SELECT count(*) FROM row_history WHERE row_id = {Quoted(id)}"));
    }

    [Fact(DisplayName = "a table rebuild that adds a column regenerates the triggers for the new column set, keeps every row and every history row, and leaves the specification marker as it was")]
    public async Task A_rebuild_regenerates_the_triggers()
    {
        await using var temp = await OpenAsync();
        var id = await InsertTaskAsync(temp);
        await new Query<HistTask>(temp.Context, HistTask.Map).Where(t => t.Id == id).Set(t => t.Name, "b").UpdateAsync();
        var marker = await temp.ScalarAsync("SELECT marker FROM orm_object_markers WHERE object_key = 'history:hist_tasks'");

        await RunAsync(temp, "0002_rebuild", new AddColumn(HistTask.Table, Col("stamped_at", ColumnType.Timestamp, false, Defaults.Now), false));
        await temp.ExecuteAsync($"UPDATE hist_tasks SET stamped_at = '2000-01-01T00:00:00.0000000Z' WHERE id = {Quoted(id)}");

        var versions = await VersionsAsync(temp, id);
        Assert.Equal(["stamped_at"], versions[0].ChangedKeys);
        Assert.Equal(["name"], versions[1].ChangedKeys);
        Assert.Equal("orm_history_hist_tasks_d,orm_history_hist_tasks_u", await TriggersAsync(temp, "hist_tasks"));
        Assert.Equal(marker, await temp.ScalarAsync("SELECT marker FROM orm_object_markers WHERE object_key = 'history:hist_tasks'"));
        Assert.Equal(1L, await temp.ScalarAsync("SELECT count(*) FROM hist_tasks"));
    }

    [Fact(DisplayName = "a native column add, a rename and a drop each regenerate the triggers around the change, and a drop or rename of a column the specification is built on is refused or followed")]
    public async Task Column_changes_follow_the_history()
    {
        await using var temp = await OpenAsync();
        var id = await InsertTaskAsync(temp);

        await RunAsync(temp, "0002_add", new AddColumn(HistTask.Table, Col("note", ColumnType.Text), false));
        await temp.ExecuteAsync($"UPDATE hist_tasks SET note = 'n' WHERE id = {Quoted(id)}");
        await RunAsync(temp, "0003_rename", new RenameColumn(HistTask.Table, "name", "title"));
        await temp.ExecuteAsync($"UPDATE hist_tasks SET title = 't' WHERE id = {Quoted(id)}");
        await RunAsync(temp, "0004_drop", new DropColumn(HistTask.Table, "status", false));
        await temp.ExecuteAsync($"UPDATE hist_tasks SET rank = 9 WHERE id = {Quoted(id)}");

        var versions = await VersionsAsync(temp, id);
        Assert.Equal([["rank"], ["title"], ["note"]], versions.Select(v => v.ChangedKeys));
        var before = await temp.ScalarAsync("SELECT group_concat(sql, ';') FROM sqlite_schema WHERE type = 'trigger' AND tbl_name = 'hist_tasks'");
        var refusal = await Assert.ThrowsAsync<NotSupportedException>(() => RunAsync(temp, "0005_bound", new DropColumn(HistTask.Table, "deleted_at", false)));
        Assert.Contains("soft-delete column", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(before, await temp.ScalarAsync("SELECT group_concat(sql, ';') FROM sqlite_schema WHERE type = 'trigger' AND tbl_name = 'hist_tasks'"));

        await RunAsync(temp, "0006_rename_key", new RenameColumn(HistTask.Table, "id", "key"));
        Assert.Equal("1:key:deleted_at:tenant_id:updated_at", await temp.ScalarAsync("SELECT marker FROM orm_object_markers WHERE object_key = 'history:hist_tasks'"));
        await temp.ExecuteAsync("UPDATE hist_tasks SET rank = 10");
        Assert.Equal(["rank"], await NewestChangedKeysUnscopedAsync(temp, id));
    }

    [Fact(DisplayName = "a trigger named like a history trigger with no specification behind it refuses a native column change by name before any statement runs")]
    public async Task Unmarked_triggers_refuse_a_native_change()
    {
        await using var temp = await OpenAsync();
        await temp.ExecuteAsync("CREATE TABLE uh_t (id TEXT PRIMARY KEY, name TEXT)");
        await temp.ExecuteAsync("CREATE TRIGGER orm_history_uh_t AFTER UPDATE ON uh_t BEGIN SELECT 1; END");

        var refusal = await Assert.ThrowsAsync<NotSupportedException>(() => RunAsync(temp, "0002_uh", new AddColumn("uh_t", Col("c", ColumnType.Text), false)));

        Assert.Contains("orm_history_uh_t", refusal.Message, StringComparison.Ordinal);
        Assert.Equal("id,name", Convert.ToString(await temp.ScalarAsync("SELECT group_concat(name) FROM pragma_table_info('uh_t')")));
    }

    [Fact(DisplayName = "installing the history twice leaves the triggers as they were, skips a table that does not exist, and stores the specification the triggers were built from")]
    public async Task The_install_is_idempotent()
    {
        await using var temp = await OpenAsync();
        var before = await temp.ScalarAsync("SELECT group_concat(sql, ';') FROM (SELECT sql FROM sqlite_schema WHERE type = 'trigger' ORDER BY name)");

        await ApplyAsync(temp, RowHistoryDdl.Install(HistoryWorld.Tables));
        await ApplyAsync(temp, new RowHistoryInstall("a.b", [new HistoryTableNode("ghost", "id", "deleted_at", "", [], "0123456789abcdef")]));

        Assert.Equal(before, await temp.ScalarAsync("SELECT group_concat(sql, ';') FROM (SELECT sql FROM sqlite_schema WHERE type = 'trigger' ORDER BY name)"));
        Assert.Equal("1:id:deleted_at:tenant_id:updated_at", await temp.ScalarAsync("SELECT marker FROM orm_object_markers WHERE object_key = 'history:hist_tasks'"));
        Assert.Equal("1:id:deleted_at::", await temp.ScalarAsync("SELECT marker FROM orm_object_markers WHERE object_key = 'history:hist_items'"));
        Assert.Equal(0L, await temp.ScalarAsync("SELECT count(*) FROM orm_object_markers WHERE object_key = 'history:ghost'"));
        Assert.Equal(1L, await temp.ScalarAsync("SELECT count(*) FROM sqlite_schema WHERE type = 'table' AND name = 'row_history'"));
    }

    [Fact(DisplayName = "the JSON image keeps integers, booleans and reals as numbers at full precision, and a blob value aborts the update that would have recorded it")]
    public async Task The_json_image_has_its_documented_limits()
    {
        await using var temp = await OpenAsync();
        await temp.ExecuteAsync("CREATE TABLE lim_t (id TEXT PRIMARY KEY, deleted_at TEXT, flag BOOLEAN, ratio REAL, big INTEGER, raw BLOB)");
        await ApplyAsync(temp, RowHistoryDdl.Install([new RowHistoryTable("lim_t", "id", "deleted_at", "", [])]));
        await temp.ExecuteAsync("INSERT INTO lim_t (id, flag, ratio, big, raw) VALUES ('r1', 1, 0.30000000000000004, 9007199254740993, NULL)");

        await temp.ExecuteAsync("UPDATE lim_t SET flag = 0, ratio = 1.5, big = 1 WHERE id = 'r1'");

        Assert.Equal("{\"flag\":1,\"ratio\":0.30000000000000004,\"big\":9007199254740993}", await temp.ScalarAsync("SELECT old_values FROM row_history WHERE source_table = 'lim_t' AND row_id = 'r1'"));
        await temp.ExecuteAsync("UPDATE lim_t SET raw = x'00ff' WHERE id = 'r1'");
        var refusal = await Assert.ThrowsAsync<SqliteException>(() => temp.ExecuteAsync("UPDATE lim_t SET raw = x'01' WHERE id = 'r1'"));
        Assert.Contains("BLOB", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(2L, await temp.ScalarAsync("SELECT count(*) FROM row_history WHERE source_table = 'lim_t'"));
    }

    [Fact(DisplayName = "an upsert that updates a tracked row is recorded like an update, and a cascade that deletes a tracked child purges its history")]
    public async Task Upserts_are_audited_and_cascades_purge()
    {
        await using var temp = await OpenAsync();
        await temp.ExecuteAsync("CREATE TABLE cas_p (id TEXT PRIMARY KEY, deleted_at TEXT)");
        await temp.ExecuteAsync("CREATE TABLE cas_c (id TEXT PRIMARY KEY, deleted_at TEXT, name TEXT, parent TEXT REFERENCES cas_p (id) ON DELETE CASCADE)");
        await ApplyAsync(temp, RowHistoryDdl.Install([new RowHistoryTable("cas_c", "id", "deleted_at", "", [])]));
        await temp.ExecuteAsync("INSERT INTO cas_p (id) VALUES ('p1')");
        await temp.ExecuteAsync("INSERT INTO cas_c (id, name, parent) VALUES ('c1', 'one', 'p1')");

        await temp.ExecuteAsync("INSERT INTO cas_c (id, name, parent) VALUES ('c1', 'two', 'p1') ON CONFLICT (id) DO UPDATE SET name = excluded.name");

        Assert.Equal("{\"name\":\"one\"}", await temp.ScalarAsync("SELECT old_values FROM row_history WHERE source_table = 'cas_c' AND row_id = 'c1'"));
        await temp.ExecuteAsync("DELETE FROM cas_p WHERE id = 'p1'");
        Assert.Equal(0L, await temp.ScalarAsync("SELECT count(*) FROM cas_c"));
        Assert.Equal(0L, await temp.ScalarAsync("SELECT count(*) FROM row_history WHERE source_table = 'cas_c'"));
    }

    [Fact(DisplayName = "a constraint name is read as an index on SQLite: a created unique index and the automatic index of a constraint have their columns in key order, a constraint name has none")]
    public async Task Constraint_columns_resolve_indexes()
    {
        await using var temp = await OpenAsync();
        await temp.ExecuteAsync("CREATE TABLE cc_t (a TEXT, b TEXT, c TEXT, CONSTRAINT uq_cc UNIQUE (b, a))");
        await temp.ExecuteAsync("CREATE INDEX ix_cc ON cc_t (c, a)");

        Assert.Equal(["c", "a"], (await ConstraintColumns.ForAsync(temp.Context, "ix_cc")).Columns);
        Assert.Equal(["b", "a"], (await ConstraintColumns.ForAsync(temp.Context, "sqlite_autoindex_cc_t_1")).Columns);
        Assert.Empty((await ConstraintColumns.ForAsync(temp.Context, "uq_cc")).Columns);
    }

    [Fact(DisplayName = "dropping a tracked table forgets its specification marker")]
    public async Task Dropping_a_table_forgets_the_marker()
    {
        await using var temp = await OpenAsync();

        await RunAsync(temp, "0002_drop", new DropTable(HistItem.Table));

        Assert.Equal(0L, await temp.ScalarAsync("SELECT count(*) FROM orm_object_markers WHERE object_key = 'history:hist_items'"));
        Assert.Equal(1L, await temp.ScalarAsync("SELECT count(*) FROM orm_object_markers WHERE object_key = 'history:hist_tasks'"));
    }

    [Fact(DisplayName = "the function that reads the transaction time returns the fixed-width UTC text of the codec, once per transaction and once per statement outside one")]
    public async Task The_transaction_time_has_the_codec_form()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        await using var connection = (OrmSqliteConnection)new SqliteDataSource(
            SqliteProviderFactory.ConnectionStringOf(new OrmLiteOptions { Provider = "sqlite", Path = temp.Path_ }), SqliteProvider.MinimumVersion).CreateConnection();
        await connection.OpenAsync();
        string At(SqliteTransaction? tx)
        {
            using var c = connection.CreateCommand();
            c.Transaction = tx;
            c.CommandText = "SELECT orm_tx_at()";
            return (string)c.ExecuteScalar()!;
        }

        var outside = At(null);
        using var transaction = connection.BeginWriteTransaction();
        var inside = At(transaction);
        System.Threading.Thread.Sleep(5);

        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{7}Z$", outside);
        Assert.Equal(inside, At(transaction));
        Assert.NotEqual(outside, inside);
        transaction.Commit();
    }

    private static class Expected
    {
        private static readonly string Uuid = SqliteHistoryDataLiterals.UuidPattern;

        internal const string Store =
            "CREATE TABLE IF NOT EXISTS row_history (id integer PRIMARY KEY AUTOINCREMENT, tenant_id text NULL, source_table text NOT NULL, row_id text NOT NULL, "
            + "op text NOT NULL CHECK (op IN ('update', 'soft_delete', 'restore')), old_values text NOT NULL CHECK (json_valid(old_values)), actor_user_id text NULL, "
            + "actor_kind text NOT NULL CHECK (actor_kind IN ('user', 'system', 'unknown')), xact_id integer NOT NULL, "
            + "changed_at text NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%f', 'now') || '0000Z'))";

        internal static readonly string Update =
            "CREATE TRIGGER \"orm_history_t_u\" AFTER UPDATE ON \"t\" WHEN OLD.\"id\" IS NOT NEW.\"id\" OR OLD.\"name\" IS NOT NEW.\"name\" OR OLD.\"deleted_at\" IS NOT NEW.\"deleted_at\" "
            + "BEGIN INSERT INTO row_history (tenant_id, source_table, row_id, op, old_values, actor_user_id, actor_kind, xact_id, changed_at) "
            + "SELECT NULL, 't', OLD.\"id\", CASE WHEN OLD.\"deleted_at\" IS NULL AND NEW.\"deleted_at\" IS NOT NULL THEN 'soft_delete' "
            + "WHEN OLD.\"deleted_at\" IS NOT NULL AND NEW.\"deleted_at\" IS NULL THEN 'restore' ELSE 'update' END, "
            + "(SELECT json_group_object(d.k, d.v) FROM (SELECT 'id' AS k, OLD.\"id\" AS v WHERE OLD.\"id\" IS NOT NEW.\"id\" "
            + "UNION ALL SELECT 'name' AS k, OLD.\"name\" AS v WHERE OLD.\"name\" IS NOT NEW.\"name\" "
            + "UNION ALL SELECT 'deleted_at' AS k, OLD.\"deleted_at\" AS v WHERE OLD.\"deleted_at\" IS NOT NEW.\"deleted_at\") AS d), "
            + "CASE WHEN lower(x.a) GLOB '" + Uuid + "' THEN lower(x.a) END, "
            + "CASE WHEN lower(x.a) GLOB '" + Uuid + "' THEN 'user' WHEN x.a = 'system' THEN 'system' ELSE 'unknown' END, "
            + "orm_tx(), orm_tx_at() FROM (SELECT orm_actor() AS a) x; END";

        internal const string Delete =
            "CREATE TRIGGER \"orm_history_t_d\" AFTER DELETE ON \"t\" BEGIN DELETE FROM row_history WHERE source_table = 't' AND row_id = OLD.\"id\"; END";
    }
}

public sealed class SqliteHistoryIdentityTests
{
    [Fact(DisplayName = "without the PostgreSQL provider the identity of a history set is a structural hash of its tables, stable for equal sets and different when a table, its key or its unversioned columns differ")]
    public void The_identity_is_structural_without_postgres()
    {
        var plain = RowHistoryDdl.SetHashWith(null, HistoryWorld.Tables);
        var other = RowHistoryDdl.SetHashWith(null, HistoryWorld.Tables.Select(t => t with { Unversioned = [.. t.Unversioned, "x"] }).ToList());
        var renamed = RowHistoryDdl.SetHashWith(null, HistoryWorld.Tables.Select(t => t with { Table = t.Table + "_2" }).ToList());

        Assert.Equal(plain, RowHistoryDdl.SetHashWith(null, [.. HistoryWorld.Tables]));
        Assert.Equal(16, plain.Length);
        Assert.NotEqual(plain, other);
        Assert.NotEqual(plain, renamed);
        Assert.NotEqual(plain, RowHistoryDdl.SetHashWith(Postgres.PostgresProvider.Instance, HistoryWorld.Tables));
    }

    [Fact(DisplayName = "the history contribution of a schema carries its nodes, so a planned provider runs it through the planner and a statement provider renders it as before")]
    public void The_history_contribution_carries_nodes()
    {
        var schema = OrmSchema.Of("Tok", EntityDescriptor.Of("Doc", "docs", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("TenantId", "tenant_id", ColumnType.Uuid, false, false)
            .Property("DeletedAt", "deleted_at", ColumnType.Timestamp, true, false)
            .SoftDelete("DeletedAt", "deleted_at", "doc").Tenant("tenant_id"));

        var history = schema.Contributions().Single(c => c.Version.StartsWith(RowHistoryDdl.VersionPrefix, StringComparison.Ordinal));

        var node = Assert.IsType<RowHistoryInstall>(Assert.Single(history.Nodes!(SqliteProvider.Instance)));
        Assert.Equal(["docs"], node.Tables.Cast<HistoryTableNode>().Select(t => t.Table));
        Assert.NotEmpty(history.Render(Postgres.PostgresProvider.Instance));
    }
}
