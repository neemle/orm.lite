// §803 provenance: item ORML-137, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class WalBusyCancellationR2AttackTests
{
    [Fact(DisplayName =
        "R2-F3 803 FULL round 2: cancel checkpoint in a proven native busy wait")]
    public async Task Native_busy_wait_cancels_and_releases_connection_and_gate()
    {
        // This fixture has no keeper connection, allowing exclusive locking.
        await using var db =
            await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        await db.ExecAsync(WalFixture.Items);

        await using var blocker = await db.RawAsync();
        Assert.Equal("exclusive", Convert.ToString(
            await SqliteMaintenanceDatabase.Scalar(
                blocker, "PRAGMA locking_mode=EXCLUSIVE")));
        Assert.Equal("wal", Convert.ToString(
            await SqliteMaintenanceDatabase.Scalar(
                blocker, "PRAGMA journal_mode=WAL")));

        await SqliteMaintenanceDatabase.Exec(
            blocker, "INSERT INTO items(v) VALUES ('committed')");

        await using var transaction = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(
            blocker,
            "INSERT INTO items(v) VALUES ('uncommitted')",
            transaction);

        // Establish the native-wait precondition with the exact checkpoint
        // statement. A returned Busy row is explicitly insufficient.
        await using (var probe = await db.RawAsync())
        {
            await using var command = probe.CreateCommand();
            command.CommandText = SqliteProvider.Instance.RenderWalCheckpoint(
                WalCheckpointMode.Truncate);
            command.CommandTimeout = 1;

            var probeClock = Stopwatch.StartNew();
            var probeFailure = await Record.ExceptionAsync(async () =>
            {
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync()) { }
            });
            probeClock.Stop();

            Assert.True(
                probeFailure is SqliteException,
                "Precondition failed: checkpoint returned a row instead of "
                + "raising native contention after a busy wait.");

            var native = Assert.IsType<SqliteException>(probeFailure);
            Assert.True(
                (native.SqliteErrorCode & 0xFF) is 5 or 6,
                "Precondition failed: the native error was not BUSY/LOCKED.");
            Assert.True(
                probeClock.Elapsed >= TimeSpan.FromMilliseconds(750),
                "Precondition failed: native contention returned immediately.");
            Assert.True(
                probeClock.Elapsed <
                    TimeSpan.FromSeconds(1) + SqliteMaintenanceDatabase.Margin,
                "The one-second native probe exceeded its explicit margin.");
        }

        await using var source = new CapturingSource(db.Context.DataSource);
        await using var context = new OrmContext(source, db.Context.Provider);
        using var cancellation = new CancellationTokenSource();
        using var hook = new StatementStartHook();
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        db.Sink.Clear();

        // Microsoft.Data.Sqlite may block synchronously inside its async API.
        var pending = Task.Run(async () =>
        {
            StatementStartHook.OnStart(() => started.TrySetResult());
            try
            {
                return await context.Maintenance.CheckpointWalAsync(
                    WalFixture.Request(WalCheckpointMode.Truncate, 30_000),
                    cancellation.Token);
            }
            finally
            {
                StatementStartHook.OnStart(null);
            }
        });

        try
        {
            await started.Task.WaitAsync(
                TimeSpan.FromSeconds(5) + SqliteMaintenanceDatabase.Margin);

            // The hook returns immediately. The external exclusive lock
            // remains held, and its effect was established by the probe.
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            Assert.False(
                pending.IsCompleted,
                "Precondition failed: checkpoint finished before cancellation.");
            Assert.False(cancellation.IsCancellationRequested);

            var limit =
                TimeSpan.FromSeconds(1) + SqliteMaintenanceDatabase.Margin;
            var cancellationClock = Stopwatch.StartNew();
            await cancellation.CancelAsync();

            var failure = await Record.ExceptionAsync(
                () => pending.WaitAsync(limit));
            cancellationClock.Stop();

            var cancelled =
                Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.Equal(cancellation.Token, cancelled.CancellationToken);
            Assert.True(
                cancellationClock.Elapsed < limit,
                "Cancellation took " + cancellationClock.Elapsed);

            // Check release before removing the external lock.
            Assert.NotNull(source.Opened);
            Assert.Equal(ConnectionState.Closed, source.Opened!.State);

            var statement = Assert.Single(db.Sink.Maintenance);
            Assert.Equal(MaintenanceOperation.WalCheckpoint, statement.Operation);
        }
        finally
        {
            await cancellation.CancelAsync();
            await transaction.RollbackAsync();

            // Exclusive locking mode retains locks beyond rollback.
            await blocker.CloseAsync();

            // Drain before disposing the database, also on assertion failure.
            await Record.ExceptionAsync(async () => { await pending; });
        }

        await db.WriteAsync().WaitAsync(
            TimeSpan.FromSeconds(5) + SqliteMaintenanceDatabase.Margin);

        var next = await db.Maintenance.CheckpointWalAsync(
            WalFixture.Request(WalCheckpointMode.Truncate, 5000))
            .WaitAsync(TimeSpan.FromSeconds(5) + SqliteMaintenanceDatabase.Margin);

        Assert.Equal(MaintenanceStatus.Completed, next.Status);
        Assert.Equal(2L, await db.LongAsync("SELECT count(*) FROM items"));
    }

    private sealed class CapturingSource(DbDataSource inner) : DbDataSource
    {
        internal DbConnection? Opened { get; private set; }

        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            throw new NotSupportedException();

        protected override async ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            var connection =
                await inner.OpenConnectionAsync(cancellationToken);
            Opened = connection;
            return connection;
        }
    }
}
