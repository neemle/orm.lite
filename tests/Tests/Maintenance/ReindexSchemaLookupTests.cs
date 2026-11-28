using System.Diagnostics;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Neemle.OrmLite.Tests.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

// ADR e8c50bcc v5 D12.5 (ORML-136 F1): a PostgreSQL index is named through its table's schema, looked up on the
// maintenance connection without a diagnostics event; and D12.4 for REINDEX on SQLite (ORML-136 F2).
public sealed class ReindexSchemaLookupTests
{
    [Theory(DisplayName = "ORML-136 D12.5 PostgreSQL qualifies the index by the looked-up schema, in both modes")]
    [InlineData(ReindexMode.Blocking, "REINDEX INDEX \"tenant\".\"ix_a_u\"")]
    [InlineData(ReindexMode.Concurrent, "REINDEX INDEX CONCURRENTLY \"tenant\".\"ix_a_u\"")]
    public void Postgres_qualifies_the_index(ReindexMode mode, string expected) =>
        Assert.Equal(expected, PostgresProvider.Instance.RenderReindex(ReindexScope.Index, mode, ReindexFixture.A, ReindexFixture.Unique, "tenant"));

    [Fact(DisplayName = "ORML-136 D12.5 PostgreSQL looks the schema up through the table's regclass; SQLite renders no lookup and ignores a schema")]
    public void The_lookups()
    {
        Assert.Equal(
            "SELECT n.nspname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid = '\"a\"'::regclass",
            PostgresProvider.Instance.RenderIndexSchemaLookup(ReindexFixture.A));
        Assert.Equal(
            "SELECT n.nspname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid = '\"o'' \"\"q\"\"\"'::regclass",
            PostgresProvider.Instance.RenderIndexSchemaLookup(Descriptors.Table("o' \"q\"")));
        Assert.Null(((IMaintenanceProvider)SqliteProvider.Instance).RenderIndexSchemaLookup(ReindexFixture.A));
        Assert.Equal("REINDEX main.\"ix_a_u\"",
            SqliteProvider.Instance.RenderReindex(ReindexScope.Index, ReindexMode.Blocking, ReindexFixture.A, ReindexFixture.Unique, "ignored"));
    }

    [PostgresFact(DisplayName = "ORML-136 D12.5 the schema lookup emits no event: an Index REINDEX on PostgreSQL reports one statement, qualified")]
    public async Task The_lookup_is_not_reported()
    {
        await using var db = await PostgresReindexFixture.OpenAsync();
        db.Sink.Clear();

        var result = await db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Index, ReindexMode.Blocking, 10_000));

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        var statement = Assert.Single(db.Sink.Maintenance);
        Assert.Equal("REINDEX INDEX \"public\".\"ix_a_u\"", statement.Text);
        Assert.Equal(MaintenanceOperation.Reindex, statement.Operation);
    }

    [PostgresFact(DisplayName = "ORML-136 D12.5 an Index REINDEX of a table that no longer exists propagates 42P01 from the lookup")]
    public async Task A_missing_table_propagates()
    {
        await using var db = await PostgresReindexFixture.OpenAsync();
        await db.ExecAsync("DROP TABLE a");
        db.Sink.Clear();

        var failure = await Assert.ThrowsAsync<Npgsql.PostgresException>(
            () => db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Index, ReindexMode.Blocking, 10_000)));

        Assert.Equal("42P01", failure.SqlState);
        Assert.Empty(db.Sink.Maintenance);
    }

    [Fact(DisplayName = "ORML-136 D12.4 a SQLite REINDEX cancelled during a lock wait throws OperationCanceledException within 1.5 s")]
    public async Task Reindex_cancellation_is_prompt()
    {
        await using var db = await ReindexFixture.OpenAsync();
        await using var blocker = await db.RawAsync();
        await using var transaction = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(blocker, "INSERT INTO b (k) VALUES ('blocker')", transaction);
        using var cancel = new CancellationTokenSource();

        var running = Task.Run(() => db.Maintenance.ReindexAsync(ReindexFixture.Request(ReindexScope.Table, timeoutMs: 10_000), cancel.Token));
        await Task.Delay(300);
        Assert.False(running.IsCompleted, "REINDEX finished before it could be cancelled in its lock wait.");
        var clock = Stopwatch.StartNew();
        await cancel.CancelAsync();
        var failure = await Record.ExceptionAsync(() => running.WaitAsync(TimeSpan.FromSeconds(10)));
        clock.Stop();
        await transaction.RollbackAsync();

        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(cancel.Token, cancelled.CancellationToken);
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(1500), "cancellation took " + clock.Elapsed);
    }
}
