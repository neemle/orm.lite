// §803 provenance: item ORML-136, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class ReindexSqliteCancellationAttackTests
{
    [Fact(DisplayName =
        "F2 803 FULL round 1: cancellation during a SQLite lock wait remains cancellation")]
    public async Task Native_lock_wait_does_not_turn_cancellation_into_busy()
    {
        await using var db = await ReindexFixture.OpenAsync();
        var before = await ReindexFixture.DumpAsync(db);

        await using var blocker = await db.RawAsync();
        await using var transaction = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(
            blocker,
            "INSERT INTO b (k) VALUES ('uncommitted blocker')",
            transaction);

        using var hook = new StatementStartHook();
        using var cancel = new CancellationTokenSource();
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var reindex = Task.Run(async () =>
        {
            StatementStartHook.OnStart(() => started.TrySetResult());
            try
            {
                return await db.Maintenance.ReindexAsync(
                    ReindexFixture.Request(
                        ReindexScope.Table,
                        ReindexMode.Blocking,
                        timeoutMs: 5000),
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
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

            // The external transaction prevents REINDEX from completing.
            // Allow execution to enter the driver's native lock retry.
            await Task.Delay(300);
            Assert.False(reindex.IsCompleted);

            await cancel.CancelAsync();

            // Keep the external lock held until cancellation or timeout.
            failure = await Record.ExceptionAsync(async () =>
            {
                await reindex.WaitAsync(TimeSpan.FromSeconds(10));
            });
        }
        finally
        {
            await transaction.RollbackAsync();
            await cancel.CancelAsync();

            // Drain the operation even when an assertion or watchdog fails.
            try
            {
                await reindex.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch
            {
            }
        }

        var next = await db.Maintenance.ReindexAsync(
            ReindexFixture.Request(ReindexScope.Table, timeoutMs: 5000));
        Assert.Equal(MaintenanceStatus.Completed, next.Status);

        await db.WriteAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(before, await ReindexFixture.DumpAsync(db));

        Assert.IsAssignableFrom<OperationCanceledException>(failure);
    }
}
