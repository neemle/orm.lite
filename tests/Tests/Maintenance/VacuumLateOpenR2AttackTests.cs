// §803 provenance: item ORML-134, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
using System.Data;
using System.Data.Common;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class VacuumLateOpenR2AttackTests
{
    [Fact(DisplayName =
        "R2-F1 803 FULL round 2: a successful open after deadline cannot start VACUUM")]
    public async Task Late_open_returns_busy_without_vacuum()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync(WalFixture.Items);
        db.Sink.Clear();

        await using var source = new LateSecondOpenSource(db.Context.DataSource);
        await using var context = new OrmContext(source, db.Context.Provider);

        var budget = TimeSpan.FromSeconds(1);
        var pending = context.Maintenance.VacuumAsync(
            new VacuumRequest(VacuumMode.Plain) { Timeout = budget });

        VacuumResult result;
        try
        {
            result = await pending.WaitAsync(
                budget + SqliteMaintenanceDatabase.Margin);
        }
        finally
        {
            source.Release.TrySetResult();
            await Record.ExceptionAsync(async () => { await pending; });
        }

        // Verify cleanup before asserting the result that fails on the head.
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);

        Assert.True(source.DeadlineObserved);
        Assert.NotNull(source.LateConnection);
        Assert.NotEqual(ConnectionState.Open, source.LateConnection!.State);
        Assert.Equal(MaintenanceStatus.Completed, result.Checkpoint?.Status);
        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.DoesNotContain(
            nameof(MaintenanceOperation.Vacuum), db.Sink.Operations);
        Assert.Equal(
            MaintenanceOperation.WalCheckpoint,
            Assert.Single(db.Sink.Maintenance).Operation);
    }

    private sealed class LateSecondOpenSource(DbDataSource inner)
        : DbDataSource
    {
        private int _opens;

        internal TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool DeadlineObserved { get; private set; }
        internal DbConnection? LateConnection { get; private set; }

        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            inner.CreateConnection();

        protected override async ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            var connection =
                await inner.OpenConnectionAsync(cancellationToken);

            if (Interlocked.Increment(ref _opens) != 2)
                return connection;

            LateConnection = connection;

            // Model native open completing successfully as cancellation wins
            // the race. Cancellation does not guarantee an exception.
            try
            {
                await Release.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                DeadlineObserved = true;
            }

            return connection;
        }
    }
}
