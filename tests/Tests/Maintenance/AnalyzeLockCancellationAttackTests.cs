// §803 provenance: item ORML-135, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Neemle.OrmLite.Tests.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class AnalyzeLockCancellationAttackTests
{
    [Fact(DisplayName =
        "F2 — 803 FULL round 1 — cancel SQLite ANALYZE after connection acquisition")]
    public async Task Sqlite_external_lock_cancellation_and_recovery()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await db.ExecAsync(WalFixture.Items);

        await using var blocker = await db.RawAsync();
        await using var transaction = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(
            blocker,
            "INSERT INTO a(k) VALUES ('cancel-blocker')",
            transaction);

        using var cancel = new CancellationTokenSource();
        using var hook = new StatementStartHook();
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // SQLite can execute synchronously inside its async API.
        // Keep cancellation on a separate execution flow.
        var running = Task.Run(async () =>
        {
            StatementStartHook.OnStart(() => started.TrySetResult());
            try
            {
                return await db.Maintenance.AnalyzeAsync(
                    new AnalyzeRequest(AnalyzeScope.Table)
                    {
                        Table = AnalyzeFixture.A,
                        Timeout = TimeSpan.FromSeconds(5)
                    },
                    cancel.Token);
            }
            finally
            {
                StatementStartHook.OnStart(null);
            }
        });

        Exception? failure = null;
        try
        {
            // This hook runs after connection acquisition, immediately
            // before executing the diagnosed maintenance statement.
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // The external write transaction remains held throughout.
            // Allow execution to reach its native lock wait.
            await Task.Delay(300);
            Assert.False(
                running.IsCompleted,
                "ANALYZE completed before cancellation could target the lock wait.");

            await cancel.CancelAsync();
            failure = await Record.ExceptionAsync(
                () => running.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            await cancel.CancelAsync();
            await transaction.CommitAsync();

            // Observe the worker before disposing the database,
            // including when an earlier assertion fails.
            await Record.ExceptionAsync(
                () => running.WaitAsync(TimeSpan.FromSeconds(10)));
        }

        Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(
            1L,
            await db.LongAsync(
                "SELECT count(*) FROM a WHERE k = 'cancel-blocker'"));

        var next = await db.Maintenance.AnalyzeAsync(
            AnalyzeFixture.Table(AnalyzeFixture.A, 5000));
        Assert.Equal(MaintenanceStatus.Completed, next.Status);

        await db.WriteAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    [PostgresFact(DisplayName =
        "F2 — 803 FULL round 1 — cancel PostgreSQL ANALYZE during an observed lock wait")]
    public async Task Postgres_external_lock_cancellation_and_recovery()
    {
        await using var db = await PostgresAnalyzeFixture.OpenAsync();
        await using var blocker = await db.RawAsync();
        await using var transaction = await blocker.BeginTransactionAsync();

        await SqliteMaintenanceDatabase.Exec(
            blocker,
            "LOCK TABLE a IN ACCESS EXCLUSIVE MODE",
            transaction);
        await SqliteMaintenanceDatabase.Exec(
            blocker,
            "INSERT INTO a VALUES (100000, 'cancel-blocker')",
            transaction);

        await using var observer = await db.RawAsync();
        await using var observation = observer.CreateCommand();
        observation.CommandTimeout = 2;
        observation.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM pg_stat_activity
                WHERE datname = current_database()
                  AND state = 'active'
                  AND wait_event_type = 'Lock'
                  AND query LIKE 'ANALYZE%'
            )
            """;

        using var cancel = new CancellationTokenSource();
        var running = db.Maintenance.AnalyzeAsync(
            new AnalyzeRequest(AnalyzeScope.Table)
            {
                Table = AnalyzeFixture.A,
                Timeout = TimeSpan.FromSeconds(15)
            },
            cancel.Token);

        Exception? failure = null;
        try
        {
            var clock = Stopwatch.StartNew();
            var waiting = false;

            while (clock.Elapsed < TimeSpan.FromSeconds(5))
            {
                if (await observation.ExecuteScalarAsync() is true)
                {
                    waiting = true;
                    break;
                }

                if (running.IsCompleted)
                    break;

                await Task.Delay(25);
            }

            Assert.True(
                waiting,
                "PostgreSQL never reported the ANALYZE lock wait.");

            await cancel.CancelAsync();
            failure = await Record.ExceptionAsync(
                () => running.WaitAsync(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            await cancel.CancelAsync();
            await transaction.CommitAsync();

            await Record.ExceptionAsync(
                () => running.WaitAsync(TimeSpan.FromSeconds(10)));
        }

        Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(
            new[] { "cancel-blocker" },
            await db.DumpAsync("SELECT k FROM a WHERE id = 100000"));

        var next = await db.Maintenance.AnalyzeAsync(
            AnalyzeFixture.Table(AnalyzeFixture.A, 5000));
        Assert.Equal(MaintenanceStatus.Completed, next.Status);

        await using var unit = await UnitOfWork.BeginAsync(db.Context);
        await SqliteMaintenanceDatabase.Exec(
            unit.Connection,
            "INSERT INTO a VALUES (100001, 'after-cancel')",
            unit.Transaction);
        await unit.CommitAsync();
    }
}
