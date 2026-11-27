using System.Diagnostics;
using Neemle.OrmLite.Tests.Nodes;
using Npgsql;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

internal static class PostgresAnalyzeFixture
{
    internal static async Task<PostgresMaintenanceDatabase> OpenAsync()
    {
        var db = await PostgresMaintenanceDatabase.CreateAsync();
        foreach (var table in new[] { "a", "b" })
        {
            await db.ExecAsync("CREATE TABLE " + table + " (id integer PRIMARY KEY, k text NOT NULL) WITH (autovacuum_enabled = false)");
            await db.ExecAsync("CREATE INDEX ix_" + table + "_k ON " + table + " (k)");
            await db.ExecAsync("INSERT INTO " + table + " SELECT g, 'k' || (g % 7) FROM generate_series(1, 300) g");
        }
        return db;
    }

    internal static Task<List<string>> AnalyzedAsync(PostgresMaintenanceDatabase db) =>
        db.DumpAsync("SELECT DISTINCT tablename FROM pg_stats WHERE schemaname = 'public' AND tablename IN ('a', 'b') ORDER BY tablename");

    internal static Task<List<string>> DumpAsync(PostgresMaintenanceDatabase db) =>
        db.DumpAsync("SELECT 'a', id, k FROM a UNION ALL SELECT 'b', id, k FROM b ORDER BY 1, 2");
}

// AC-135-04 and AC-135-09 on PostgreSQL.
public sealed class PostgresAnalyzeTests
{
    [PostgresFact(DisplayName = "AC-135-04 Table scope fills pg_stats for A only, Database scope for both; the data is unchanged")]
    public async Task Table_then_database()
    {
        await using var db = await PostgresAnalyzeFixture.OpenAsync();
        var before = await PostgresAnalyzeFixture.DumpAsync(db);
        Assert.Empty(await PostgresAnalyzeFixture.AnalyzedAsync(db));

        var table = await db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(AnalyzeFixture.A));
        var afterTable = await PostgresAnalyzeFixture.AnalyzedAsync(db);
        var database = await db.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database));

        Assert.Equal(MaintenanceStatus.Completed, table.Status);
        Assert.Equal(["a"], afterTable);
        Assert.Equal(MaintenanceStatus.Completed, database.Status);
        Assert.Equal(["a", "b"], await PostgresAnalyzeFixture.AnalyzedAsync(db));
        Assert.Equal(before, await PostgresAnalyzeFixture.DumpAsync(db));
        Assert.Equal(["ANALYZE \"a\"", "ANALYZE"], db.Sink.Maintenance.Select(s => s.Text));
    }

    [PostgresFact(DisplayName = "AC-135-09 a table that does not exist propagates SQLSTATE 42P01 unwrapped")]
    public async Task A_missing_table_propagates()
    {
        await using var db = await PostgresAnalyzeFixture.OpenAsync();

        var failure = await Assert.ThrowsAsync<PostgresException>(() => db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(Descriptors.Table("no_such_table"))));

        Assert.Equal("42P01", failure.SqlState);
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(AnalyzeFixture.A))).Status);
    }
}

// AC-135-06 and AC-135-07 on PostgreSQL: lock contention is Busy; no gate and no holder refusal.
public sealed class PostgresAnalyzeContentionTests
{
    [PostgresFact(DisplayName = "AC-135-06 a table locked ACCESS EXCLUSIVE makes Table-scope ANALYZE Busy within the timeout plus 2 s with one event; the blocker commits normally")]
    public async Task A_locked_table_is_busy()
    {
        await using var db = await PostgresAnalyzeFixture.OpenAsync();
        await using var blocker = await db.RawAsync();
        await using var locked = await blocker.BeginTransactionAsync();
        await SqliteMaintenanceDatabase.Exec(blocker, "LOCK TABLE a IN ACCESS EXCLUSIVE MODE", locked);
        db.Sink.Clear();

        var clock = Stopwatch.StartNew();
        var result = await db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(AnalyzeFixture.A, 1000));
        clock.Stop();

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1) + SqliteMaintenanceDatabase.Margin, "took " + clock.Elapsed);
        Assert.Single(db.Sink.Maintenance);
        await SqliteMaintenanceDatabase.Exec(blocker, "INSERT INTO a VALUES (100000, 'blocker')", locked);
        await locked.CommitAsync();
        Assert.Equal(1L, Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM a WHERE k = 'blocker'"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [PostgresFact(DisplayName = "AC-135-07 PostgreSQL takes no gate: ANALYZE inside an open unit of work on the same database is not refused")]
    public async Task No_holder_refusal_on_postgres()
    {
        await using var db = await PostgresAnalyzeFixture.OpenAsync();
        await using var unit = await UnitOfWork.BeginAsync(db.Context);

        var result = await db.Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database) { Timeout = TimeSpan.FromSeconds(10) });

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        await unit.CommitAsync();
    }
}
