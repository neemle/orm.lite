using System.Data.Common;
using System.Globalization;
using Neemle.Orm.Lite.Nodes;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public sealed class PostgresMigrationLockTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string History = "SELECT to_regclass('schema_migrations')::text";

    private OrmContext Ctx => fixture.Context;

    private static MigrationContribution[] Contributions() =>
    [
        MigrationContribution.Of(
            "3000_lk_table",
            Reversibility.Reversible,
            [new CreateTable("lk_t", NodeList.Of([new ColumnDef("id", new TypeRef(ColumnType.Text), false, true)]))]),
    ];

    private static async Task ExecAsync(DbConnection connection, string statement)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        await command.ExecuteNonQueryAsync();
    }

    private async Task<object?> ScalarAsync(string statement)
    {
        var rows = await fixture.RowsAsync(statement);
        return rows[0][0];
    }

    private async Task WaitForWaiterAsync()
    {
        var waiting = "SELECT count(*) FROM pg_locks WHERE locktype = 'advisory' AND NOT granted "
            + $"AND objid = {MigrationRunner.LockKey} "
            + "AND database = (SELECT oid FROM pg_database WHERE datname = current_database())";
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (Convert.ToInt64(await ScalarAsync(waiting), CultureInfo.InvariantCulture) > 0) return;
            await Task.Delay(50);
        }
        Assert.Fail("the runner never waited for the migration lock");
    }

    [SkippableFact(DisplayName = "a runner that waits for the migration lock has not created the history table yet, "
        + "and creates it once the lock is free")]
    public async Task A_runner_waiting_for_the_lock_has_not_created_the_history_table()
    {
        fixture.RequireAvailable();
        await using var holder = await Ctx.DataSource.OpenConnectionAsync();
        await ExecAsync(holder, $"SELECT pg_advisory_lock({MigrationRunner.LockKey})");
        var run = Task.Run(() => MigrationRunner.RunAsync(Ctx.DataSource, Ctx.Provider, Contributions()));
        try
        {
            await WaitForWaiterAsync();
            Assert.False(run.IsCompleted);
            Assert.Null(await ScalarAsync(History));
        }
        finally
        {
            await ExecAsync(holder, $"SELECT pg_advisory_unlock({MigrationRunner.LockKey})");
        }

        await run;

        var recorded = await ScalarAsync("SELECT count(*) FROM schema_migrations WHERE version = '3000_lk_table'");
        Assert.Equal("schema_migrations", (string?)await ScalarAsync(History));
        Assert.Equal(1L, Convert.ToInt64(recorded, CultureInfo.InvariantCulture));
    }
}
