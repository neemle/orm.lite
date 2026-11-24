using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Sqlite;
using Neemle.OrmLite.Tests.Behaviour;
using Xunit;

namespace Neemle.OrmLite.Tests.SqliteRules;

public sealed class SqliteHistoryEdgeTests
{
    private static async Task<TempSqlite> OpenAsync()
    {
        var temp = await TempSqlite.OpenAsync(schema: false);
        HistoryWorld.Register();
        await ApplyAsync(temp, [.. HistoryWorld.Schema()]);
        await ApplyAsync(temp, RowHistoryDdl.Install(HistoryWorld.Tables));
        return temp;
    }

    private static Task ApplyAsync(TempSqlite temp, params Node[] operations) =>
        MigrationRunner.ApplyAsync(temp.Context.DataSource, temp.Context.Provider, operations);

    private static Task<object?> TriggersAsync(TempSqlite temp, string table) => temp.ScalarAsync(
        "SELECT group_concat(name) FROM (SELECT name FROM sqlite_schema "
        + "WHERE type = 'trigger' AND tbl_name = '" + table + "' ORDER BY name)");

    private static AddColumn Stamp() => new(
        HistTask.Table,
        new ColumnDef("stamped_at", new TypeRef(ColumnType.Timestamp), false, false, Defaults.Now),
        false);

    [Fact(DisplayName = "a marker with a colon or a comma in a column name round-trips through version 2")]
    public void A_marker_with_separators_round_trips()
    {
        var spec = new SqliteHistorySpec("a:b", "d%3Ax", "t,1", ["x,y", "z"]);

        var marker = spec.Encode();
        var decoded = SqliteHistorySpec.Decode(marker);

        Assert.Equal("2:a%3Ab:d%253Ax:t%2C1:x%2Cy,z", marker);
        Assert.NotNull(decoded);
        Assert.Equal(("a:b", "d%3Ax", "t,1"), (decoded.KeyColumn, decoded.SoftColumn, decoded.TenantColumn));
        Assert.Equal(["x,y", "z"], decoded.Unversioned);
    }

    [Fact(DisplayName = "a marker with ordinary names keeps its version 1 text, and a version 1 marker still reads")]
    public void An_ordinary_marker_stays_version_one()
    {
        var spec = new SqliteHistorySpec("id", "deleted_at", null, ["updated_at", "x%y"]);

        Assert.Equal("1:id:deleted_at::updated_at,x%y", spec.Encode());
        var decoded = SqliteHistorySpec.Decode("1:id:deleted_at::updated_at,x%y");
        Assert.NotNull(decoded);
        Assert.Equal(["updated_at", "x%y"], decoded.Unversioned);
        Assert.Null(decoded.TenantColumn);
        Assert.Null(SqliteHistorySpec.Decode("3:a:b:c:d"));
    }

    [Fact(DisplayName = "a user trigger that only shares the history prefix survives a rebuild and an install")]
    public async Task A_prefixed_user_trigger_is_not_owned()
    {
        await using var temp = await OpenAsync();
        await temp.ExecuteAsync("CREATE TABLE hist_note_log (id TEXT)");
        await temp.ExecuteAsync(
            "CREATE TRIGGER orm_history_audit AFTER INSERT ON hist_tasks "
            + "BEGIN INSERT INTO hist_note_log (id) VALUES (new.id); END");
        var user = await temp.ScalarAsync("SELECT sql FROM sqlite_schema WHERE name = 'orm_history_audit'");

        await ApplyAsync(temp, Stamp());
        await ApplyAsync(temp, RowHistoryDdl.Install(HistoryWorld.Tables));

        var expected = "orm_history_audit,orm_history_hist_tasks_d,orm_history_hist_tasks_u";
        Assert.Equal(expected, await TriggersAsync(temp, "hist_tasks"));
        Assert.Equal(user, await temp.ScalarAsync("SELECT sql FROM sqlite_schema WHERE name = 'orm_history_audit'"));
    }

    [Fact(DisplayName = "converting one foreign key works beside another that is ON DELETE SET DEFAULT")]
    public async Task Another_foreign_key_may_use_set_default()
    {
        await using var temp = await OpenAsync();
        await temp.ExecuteAsync("CREATE TABLE ed_p (id TEXT PRIMARY KEY)");
        await temp.ExecuteAsync(
            "CREATE TABLE ed_c (id TEXT PRIMARY KEY, a TEXT, b TEXT, "
            + "CONSTRAINT fk_a FOREIGN KEY (a) REFERENCES ed_p (id) ON DELETE SET DEFAULT, "
            + "CONSTRAINT fk_b FOREIGN KEY (b) REFERENCES ed_p (id) ON DELETE CASCADE)");
        var column = NodeList.Of([new ColumnRef("b")]);
        var parent = NodeList.Of([new ColumnRef("id")]);

        await ApplyAsync(temp, new ConvertForeignKeyAction(
            "ed_c", "fk_b", column, "ed_p", parent,
            ReferentialAction.SetNull, ReferentialAction.Cascade, ReferentialAction.NoAction));

        Assert.Equal("SET DEFAULT,SET NULL", await temp.ScalarAsync(
            "SELECT group_concat(on_delete) FROM "
            + "(SELECT on_delete FROM pragma_foreign_key_list('ed_c') ORDER BY \"from\")"));
        await using var connection = await temp.Context.DataSource.OpenConnectionAsync();
        var refusal = await Assert.ThrowsAsync<NotSupportedException>(
            () => temp.Context.Provider.CreateCatalog(connection).ForeignKeysAsync("ed_c"));
        Assert.Contains("ed_c", refusal.Message, StringComparison.Ordinal);
    }
}
