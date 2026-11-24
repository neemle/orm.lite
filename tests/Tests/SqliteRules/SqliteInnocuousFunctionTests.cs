using System.Data.Common;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.SqliteRules;

public sealed class SqliteInnocuousFunctionTests
{
    private static async Task<DbConnection> HardenedAsync(TempSqlite temp)
    {
        var connection = await temp.Context.DataSource.OpenConnectionAsync();
        await RunAsync(connection, "PRAGMA trusted_schema = OFF");
        return connection;
    }

    private static async Task<object?> RunAsync(DbConnection connection, string statement)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        return await command.ExecuteScalarAsync();
    }

    [Fact(DisplayName = "a decimal CHECK runs with trusted_schema off and still refuses a value that does not fit")]
    public async Task The_decimal_check_runs_with_trusted_schema_off()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        await using var connection = await HardenedAsync(temp);
        await RunAsync(connection, "CREATE TABLE in_dec (id TEXT PRIMARY KEY, amount TEXT COLLATE orm_decimal "
            + "CHECK (orm_decimal_fits(amount, 10, 2)))");

        await RunAsync(connection, "INSERT INTO in_dec (id, amount) VALUES ('a', '1.10')");

        Assert.Equal("1.10", await RunAsync(connection, "SELECT amount FROM in_dec WHERE id = 'a'"));
        var refusal = await Assert.ThrowsAsync<SqliteException>(
            () => RunAsync(connection, "INSERT INTO in_dec (id, amount) VALUES ('b', '1.234')"));
        Assert.Contains("CHECK constraint failed", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("unsafe use", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "views over the json, ilike and decimal aggregate functions read with trusted_schema off")]
    public async Task Views_over_library_functions_run_with_trusted_schema_off()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        await using var connection = await HardenedAsync(temp);
        await RunAsync(
            connection, "CREATE TABLE in_doc (id TEXT PRIMARY KEY, doc TEXT, amount TEXT COLLATE orm_decimal)");
        await RunAsync(connection, "INSERT INTO in_doc (id, doc, amount) VALUES ('a1', '{\"k\":\"v\"}', '1.10'), "
            + "('b2', '{\"k\":\"w\"}', '2.20')");
        await RunAsync(connection, "CREATE VIEW in_json AS SELECT id, orm_json_text(doc, 'k') AS k, "
            + "orm_json_has(doc, 'k') AS has_k FROM in_doc");
        await RunAsync(connection, "CREATE VIEW in_like AS SELECT id FROM in_doc WHERE orm_ilike(id, 'A%', NULL)");
        await RunAsync(connection, "CREATE VIEW in_total AS SELECT orm_decimal_sum(amount) AS total, "
            + "orm_decimal_average(amount, 2) AS mean FROM in_doc");

        Assert.Equal("v", await RunAsync(connection, "SELECT k FROM in_json WHERE id = 'a1'"));
        Assert.Equal(1L, await RunAsync(connection, "SELECT has_k FROM in_json WHERE id = 'b2'"));
        Assert.Equal("a1", await RunAsync(connection, "SELECT id FROM in_like"));
        Assert.Equal("3.30", await RunAsync(connection, "SELECT total FROM in_total"));
        Assert.Equal("1.65", await RunAsync(connection, "SELECT mean FROM in_total"));
    }

    [Fact(DisplayName = "a library function that fails reports its own message, not a registration or type error")]
    public async Task A_failing_function_reports_its_message()
    {
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        await using var connection = await temp.Context.DataSource.OpenConnectionAsync();

        var refusal = await Assert.ThrowsAsync<SqliteException>(
            () => RunAsync(connection, "SELECT orm_json_remove('1', 'k')"));

        Assert.Contains("cannot delete from scalar", refusal.Message, StringComparison.Ordinal);
    }
}
