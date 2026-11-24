using System.Globalization;
using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Tests.Behaviour;
using Xunit;

namespace Neemle.Orm.Lite.Tests.SqliteRules;

public sealed class SqliteRebuildTriggerTests(SqliteFixture fixture) : IClassFixture<SqliteFixture>
{
    private static ColumnDef Stamp => new("stamped_at", new TypeRef(ColumnType.Timestamp), false, false, Defaults.Now);

    private Task RebuildAsync(string table) =>
        MigrationRunner.ApplyAsync(fixture.Context.DataSource, fixture.Context.Provider, [new AddColumn(table, Stamp)]);

    private async Task<string> RowsAsync(string statement)
    {
        var rows = await fixture.RowsAsync(statement);
        return string.Join(" ; ", rows.Select(r => string.Join("|", r.Select(Text))));
    }

    private static string? Text(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture);

    private Task<string> TriggersAsync(string table) => RowsAsync(
        "SELECT name FROM sqlite_schema WHERE type = 'trigger' AND tbl_name = '" + table + "' ORDER BY name");

    [Fact(DisplayName = "a rebuild recreates the views before the INSTEAD OF trigger that is defined on one")]
    public async Task A_trigger_on_a_view_is_recreated_after_the_view()
    {
        await fixture.ExecuteAsync("CREATE TABLE ra_x (id TEXT PRIMARY KEY, n INTEGER)");
        await fixture.ExecuteAsync("CREATE VIEW ra_v AS SELECT * FROM ra_x");
        await fixture.ExecuteAsync(
            "CREATE TRIGGER ra_tv INSTEAD OF INSERT ON ra_v "
            + "BEGIN INSERT INTO ra_x (id, n) VALUES (new.id, new.n); END");

        await RebuildAsync("ra_x");

        Assert.Equal("ra_tv", await TriggersAsync("ra_v"));
        await fixture.ExecuteAsync("INSERT INTO ra_v (id, n) VALUES ('a', 1)");
        Assert.Equal("a|1", await RowsAsync("SELECT id, n FROM ra_x"));
    }

    [Fact(DisplayName = "a rebuild keeps an INSTEAD OF trigger on a view that never mentions the rebuilt table")]
    public async Task An_unrelated_view_trigger_survives()
    {
        await fixture.ExecuteAsync("CREATE TABLE rb_x (id TEXT PRIMARY KEY)");
        await fixture.ExecuteAsync("CREATE TABLE rb_other (id TEXT PRIMARY KEY)");
        await fixture.ExecuteAsync("CREATE TABLE rb_log (id TEXT)");
        await fixture.ExecuteAsync("CREATE VIEW rb_v AS SELECT id FROM rb_other");
        await fixture.ExecuteAsync(
            "CREATE TRIGGER rb_tv INSTEAD OF DELETE ON rb_v BEGIN INSERT INTO rb_log (id) VALUES (old.id); END");

        await RebuildAsync("rb_x");

        Assert.Equal("rb_tv", await TriggersAsync("rb_v"));
        await fixture.ExecuteAsync("INSERT INTO rb_other (id) VALUES ('o')");
        await fixture.ExecuteAsync("DELETE FROM rb_v WHERE id = 'o'");
        Assert.Equal("o", await RowsAsync("SELECT id FROM rb_log"));
    }

    [Fact(DisplayName = "a rebuild succeeds and keeps a trigger on another table whose body reads a view")]
    public async Task A_trigger_that_reads_a_view_survives()
    {
        await fixture.ExecuteAsync("CREATE TABLE rc_x (id TEXT PRIMARY KEY)");
        await fixture.ExecuteAsync("CREATE TABLE rc_other (id TEXT PRIMARY KEY)");
        await fixture.ExecuteAsync("CREATE TABLE rc_y (id TEXT)");
        await fixture.ExecuteAsync("CREATE TABLE rc_log (id TEXT)");
        await fixture.ExecuteAsync("CREATE VIEW rc_v AS SELECT id FROM rc_other");
        await fixture.ExecuteAsync(
            "CREATE TRIGGER rc_ty AFTER INSERT ON rc_y BEGIN INSERT INTO rc_log (id) SELECT id FROM rc_v; END");

        await RebuildAsync("rc_x");

        Assert.Equal("rc_ty", await TriggersAsync("rc_y"));
        await fixture.ExecuteAsync("INSERT INTO rc_other (id) VALUES ('seen')");
        await fixture.ExecuteAsync("INSERT INTO rc_y (id) VALUES ('go')");
        Assert.Equal("seen", await RowsAsync("SELECT id FROM rc_log"));
    }
}
