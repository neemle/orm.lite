using System.Data.Common;
using System.Diagnostics;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

// ADR e8c50bcc v5 D12.4 (ORML-135 F2, ORML-136 F2, ORML-137 F4): cancellation wins over contention, and SQLite waits
// for a lock one retry slice at a time inside one diagnosed statement.
public sealed class MaintenanceCancellationTests
{
    [Fact(DisplayName = "ORML-135 D12.4 a SQLite ANALYZE cancelled during a lock wait throws OperationCanceledException within 1.5 s")]
    public async Task Analyze_cancellation_is_prompt()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await using var blocker = await db.RawAsync();
        await using var transaction = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(blocker, "INSERT INTO a (k) VALUES ('blocker')", transaction);
        using var cancel = new CancellationTokenSource();

        var running = Task.Run(() => db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(AnalyzeFixture.A, 10_000), cancel.Token));
        await Task.Delay(300);
        Assert.False(running.IsCompleted, "ANALYZE finished before it could be cancelled in its lock wait.");
        var clock = Stopwatch.StartNew();
        await cancel.CancelAsync();
        var failure = await Record.ExceptionAsync(() => running.WaitAsync(TimeSpan.FromSeconds(10)));
        clock.Stop();
        await transaction.RollbackAsync();

        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(cancel.Token, cancelled.CancellationToken);
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(1500), "cancellation took " + clock.Elapsed);
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(AnalyzeFixture.A, 5000))).Status);
    }

    [Fact(DisplayName = "ORML-137 F4 D12.4 a SQLite lock that clears after several retry slices completes as one Maintenance event")]
    public async Task Retries_stay_one_event()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await using var blocker = await db.RawAsync();
        var transaction = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(blocker, "INSERT INTO a (k) VALUES ('blocker')", transaction);
        db.Sink.Clear();

        var running = Task.Run(() => db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(AnalyzeFixture.A, 10_000)));
        await Task.Delay(2300);
        Assert.False(running.IsCompleted);
        await transaction.CommitAsync();
        await transaction.DisposeAsync();
        var result = await running.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(MaintenanceStatus.Completed, result.Status);
        var statement = Assert.Single(db.Sink.Maintenance);
        Assert.Equal(MaintenanceOperation.Analyze, statement.Operation);
        Assert.Null(statement.Error);
        Assert.True(statement.Elapsed >= TimeSpan.FromSeconds(2), "the event covers " + statement.Elapsed);
    }

    [Fact(DisplayName = "ORML-135 D12.4 a SQLite lock that outlasts the budget is Busy, reported as one Maintenance event")]
    public async Task Retries_end_with_the_budget()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await using var blocker = await db.RawAsync();
        await using var transaction = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(blocker, "INSERT INTO a (k) VALUES ('blocker')", transaction);
        db.Sink.Clear();

        var clock = Stopwatch.StartNew();
        var result = await db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(AnalyzeFixture.A, 1500));
        clock.Stop();
        await transaction.RollbackAsync();

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Single(db.Sink.Maintenance);
        Assert.True(clock.Elapsed < TimeSpan.FromMilliseconds(1500) + SqliteMaintenanceDatabase.Margin, "Busy took " + clock.Elapsed);
    }

    [Fact(DisplayName = "ORML-135 D12.4 a Busy outcome after the token was cancelled throws OperationCanceledException")]
    public async Task Busy_after_cancellation_is_cancellation()
    {
        using var cancel = new CancellationTokenSource();
        var context = new OrmContext(new NeverUsedDataSource(), new CancellingGatePort(cancel, null));

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000), cancel.Token));

        Assert.Equal(cancel.Token, failure.CancellationToken);
        Assert.Null(failure.InnerException);
    }

    [Fact(DisplayName = "ORML-135 D12.4 a failure after the token was cancelled is cancellation, with the failure inside")]
    public async Task Failure_after_cancellation_is_cancellation()
    {
        using var cancel = new CancellationTokenSource();
        var inner = new InvalidOperationException("the gate failed");
        var context = new OrmContext(new NeverUsedDataSource(), new CancellingGatePort(cancel, inner));

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000), cancel.Token));

        Assert.Equal(cancel.Token, failure.CancellationToken);
        Assert.Same(inner, failure.InnerException);
    }

    [Fact(DisplayName = "ORML-135 D12.4 a failure while the token is live propagates unchanged")]
    public async Task Failure_without_cancellation_propagates()
    {
        var inner = new InvalidOperationException("the gate failed");
        var context = new OrmContext(new NeverUsedDataSource(), new CancellingGatePort(null, inner));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000)));

        Assert.Same(inner, failure);
    }

    [Theory(DisplayName = "ORML-135 D12.4 with a retry slice the command timeout is the slice or the budget left, whichever is shorter, at least 1 s")]
    [InlineData(30_000, 1_000, 1)]
    [InlineData(300, 1_000, 1)]
    [InlineData(3_400, 10_000, 4)]
    [InlineData(30_000, 2_500, 3)]
    public void The_slice_bounds_the_command_timeout(int budgetMs, int sliceMs, int seconds) =>
        Assert.Equal(seconds, new MaintenanceBudget(TimeSpan.FromMilliseconds(budgetMs)).CommandSeconds(0, TimeSpan.FromMilliseconds(sliceMs)));

    [Fact(DisplayName = "ORML-135 D12.4 SQLite takes lock waits one second at a time; PostgreSQL and the default port do not slice")]
    public void Retry_slices()
    {
        Assert.Equal(TimeSpan.FromSeconds(1), SqliteProvider.Instance.ContentionRetrySlice);
        Assert.Null(((IMaintenanceProvider)PostgresProvider.Instance).ContentionRetrySlice);
    }

    // SQLite's checkpoint port, except that its gate cancels the caller's token and then reports a busy gate or fails.
    private sealed class CancellingGatePort(CancellationTokenSource? cancel, Exception? failure) : ISqlProvider, IMaintenanceProvider
    {
        public string Id => "cancelling-gate";

        public CapabilitySet Capabilities => SqliteProvider.Instance.Capabilities;

        public ISerializer CreateSerializer() => SqliteProvider.Instance.CreateSerializer();

        public TimeSpan MaintenanceTimeout => TimeSpan.FromSeconds(5);

        public string? WriterKeyOf(DbDataSource source) => null;

        public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(DbDataSource source, TimeSpan timeout, CancellationToken ct)
        {
            cancel?.Cancel();
            if (failure is not null) throw failure;
            return ValueTask.FromResult(new MaintenanceWriteLease(false, null));
        }

        public string RenderWalCheckpoint(WalCheckpointMode mode) => SqliteProvider.Instance.RenderWalCheckpoint(mode);
    }
}
