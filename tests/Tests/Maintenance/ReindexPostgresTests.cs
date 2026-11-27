using System.Diagnostics;
using Neemle.OrmLite.Tests.Nodes;
using Npgsql;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

internal static class PostgresReindexFixture
{
    internal static readonly string[] Indexes = ["a_pkey", "ix_a_u", "ix_a_k_live", "b_pkey", "ix_b_k"];

    internal static async Task<PostgresMaintenanceDatabase> OpenAsync()
    {
        var db = await PostgresMaintenanceDatabase.CreateAsync();
        await db.ExecAsync("CREATE TABLE a (id integer PRIMARY KEY, u text NOT NULL, k text NOT NULL, deleted_at timestamptz NULL)");
        await db.ExecAsync("CREATE UNIQUE INDEX ix_a_u ON a (u)");
        await db.ExecAsync("CREATE INDEX ix_a_k_live ON a (k) WHERE deleted_at IS NULL");
        await db.ExecAsync("CREATE TABLE b (id integer PRIMARY KEY, k text NOT NULL)");
        await db.ExecAsync("CREATE INDEX ix_b_k ON b (k)");
        await db.ExecAsync("INSERT INTO a SELECT g, 'u' || g, 'k' || (g % 5), CASE WHEN g % 4 = 0 THEN now() END FROM generate_series(1, 500) g");
        await db.ExecAsync("INSERT INTO b SELECT g, 'k' || (g % 3) FROM generate_series(1, 500) g");
        return db;
    }

    internal static Task<List<string>> DumpAsync(PostgresMaintenanceDatabase db) =>
        db.DumpAsync("SELECT 'a', id, u, k, deleted_at IS NULL FROM a UNION ALL SELECT 'b', id, k, NULL, NULL FROM b ORDER BY 1, 2");

    internal static async Task<Dictionary<string, long>> NodesAsync(PostgresMaintenanceDatabase db)
    {
        var nodes = new Dictionary<string, long>();
        foreach (var index in Indexes) nodes[index] = await db.RelFileNodeAsync(index);
        return nodes;
    }
}

// AC-136-03 and AC-136-04 on PostgreSQL: every scope in both modes keeps the data and rebuilds exactly its indexes.
public sealed class PostgresReindexRebuildTests
{
    [PostgresFact(DisplayName = "AC-136-03/04 every scope in both modes keeps the data and leaves every index valid; Index rebuilds only its index, Table every index of A, Database those of A and B")]
    public async Task Each_scope_and_mode()
    {
        await using var db = await PostgresReindexFixture.OpenAsync();
        var before = await PostgresReindexFixture.DumpAsync(db);
        var expected = new Dictionary<ReindexScope, string[]>
        {
            [ReindexScope.Index] = ["ix_a_u"],
            [ReindexScope.Table] = ["a_pkey", "ix_a_u", "ix_a_k_live"],
            [ReindexScope.Database] = PostgresReindexFixture.Indexes,
        };

        foreach (var mode in new[] { ReindexMode.Blocking, ReindexMode.Concurrent })
            foreach (var (scope, rebuilt) in expected)
            {
                var nodes = await PostgresReindexFixture.NodesAsync(db);

                var result = await db.Maintenance.ReindexAsync(ReindexFixture.Request(scope, mode, 30000));

                Assert.Equal(MaintenanceStatus.Completed, result.Status);
                var after = await PostgresReindexFixture.NodesAsync(db);
                foreach (var index in PostgresReindexFixture.Indexes)
                    Assert.True(rebuilt.Contains(index) == (nodes[index] != after[index]), scope + "/" + mode + ": " + index);
                Assert.Equal(before, await PostgresReindexFixture.DumpAsync(db));
                Assert.Equal(0L, Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM pg_index i JOIN pg_class c ON c.oid = i.indrelid WHERE c.relname IN ('a', 'b') AND NOT i.indisvalid"), System.Globalization.CultureInfo.InvariantCulture));
                var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.ExecAsync("INSERT INTO a VALUES (100000, 'u1', 'x', NULL)"));
                Assert.Equal("23505", duplicate.SqlState);
            }
        Assert.All(db.Sink.Maintenance, s => Assert.Null(s.TransactionId));
    }

    [PostgresFact(DisplayName = "AC-136-10 an index dropped after its descriptor was generated propagates the server's error unwrapped")]
    public async Task A_missing_index_propagates()
    {
        await using var db = await PostgresReindexFixture.OpenAsync();
        await db.ExecAsync("DROP INDEX ix_a_k_live");

        var failure = await Assert.ThrowsAsync<PostgresException>(() => db.Maintenance.ReindexAsync(
            new ReindexRequest(ReindexScope.Index, ReindexMode.Blocking) { Table = ReindexFixture.A, Index = ReindexFixture.Live }));

        Assert.Equal("42P01", failure.SqlState);
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Table))).Status);
    }
}

// AC-136-08: blocking contention.
public sealed class PostgresReindexContentionTests
{
    [PostgresFact(DisplayName = "AC-136-08 an uncommitted INSERT into A makes a blocking Table REINDEX of A Busy within the timeout plus 2 s; the writer then commits and the data matches")]
    public async Task A_writer_makes_blocking_reindex_busy()
    {
        await using var db = await PostgresReindexFixture.OpenAsync();
        await using var writer = await db.RawAsync();
        await using var open = await writer.BeginTransactionAsync();
        await SqliteMaintenanceDatabase.Exec(writer, "INSERT INTO a VALUES (100001, 'writer', 'kw', NULL)", open);

        var clock = Stopwatch.StartNew();
        var result = await db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Table, ReindexMode.Blocking, 1000));
        clock.Stop();

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1) + SqliteMaintenanceDatabase.Margin, "took " + clock.Elapsed);
        await open.CommitAsync();
        Assert.Equal(501L, Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM a"), System.Globalization.CultureInfo.InvariantCulture));
    }
}

// AC-136-09: CONCURRENTLY lets reads and writes run.
public sealed class PostgresReindexConcurrentTests
{
    [PostgresFact(DisplayName = "AC-136-09 while a concurrent Table REINDEX of A waits for an older transaction, a SELECT and an INSERT on A from another connection complete")]
    public async Task Reads_and_writes_continue()
    {
        await using var db = await PostgresReindexFixture.OpenAsync();
        await using var older = await db.RawAsync();
        await using var open = await older.BeginTransactionAsync();
        await SqliteMaintenanceDatabase.Exec(older, "INSERT INTO a VALUES (100002, 'older', 'ko', NULL)", open);

        var reindex = Task.Run(() => db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Table, ReindexMode.Concurrent, 60000)));
        await WaitUntilRunningAsync(db);

        await using (var other = await db.RawAsync())
        {
            await using var select = other.CreateCommand();
            select.CommandText = "SELECT count(*) FROM a";
            select.CommandTimeout = 5;
            Assert.Equal(500L, Convert.ToInt64(await select.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
            await using var insert = other.CreateCommand();
            insert.CommandText = "INSERT INTO a VALUES (100003, 'during', 'kd', NULL)";
            insert.CommandTimeout = 5;
            Assert.Equal(1, await insert.ExecuteNonQueryAsync());
        }
        Assert.False(reindex.IsCompleted);

        await open.CommitAsync();
        Assert.Equal(MaintenanceStatus.Completed, (await reindex.WaitAsync(TimeSpan.FromSeconds(30))).Status);
        Assert.Equal(0L, Convert.ToInt64(await db.ScalarAsync("SELECT count(*) FROM pg_index i JOIN pg_class c ON c.oid = i.indrelid WHERE c.relname = 'a' AND NOT i.indisvalid"), System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task WaitUntilRunningAsync(PostgresMaintenanceDatabase db)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed < TimeSpan.FromSeconds(15))
        {
            var waiting = Convert.ToInt64(await db.ScalarAsync(
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND query LIKE 'REINDEX TABLE CONCURRENTLY%' AND wait_event_type = 'Lock'"),
                System.Globalization.CultureInfo.InvariantCulture);
            if (waiting > 0) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("the concurrent REINDEX never waited for the older transaction");
    }
}

// AC-136-11 on PostgreSQL: cancelling a lock wait.
public sealed class PostgresReindexCancellationTests
{
    [PostgresFact(DisplayName = "AC-136-11 cancelling a REINDEX that waits for a lock cancels it; the next REINDEX succeeds and the data is unchanged")]
    public async Task Cancelling_the_lock_wait()
    {
        await using var db = await PostgresReindexFixture.OpenAsync();
        var before = await PostgresReindexFixture.DumpAsync(db);
        await using (var blocker = await db.RawAsync())
        {
            await using var locked = await blocker.BeginTransactionAsync();
            await SqliteMaintenanceDatabase.Exec(blocker, "LOCK TABLE a IN ACCESS EXCLUSIVE MODE", locked);
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Table, ReindexMode.Blocking, 30000), cancel.Token));
            await locked.CommitAsync();
        }

        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Table))).Status);
        Assert.Equal(before, await PostgresReindexFixture.DumpAsync(db));
    }
}
