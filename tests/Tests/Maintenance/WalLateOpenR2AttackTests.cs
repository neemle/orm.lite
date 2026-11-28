// §803 provenance: item ORML-137, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class WalLateOpenR2AttackTests
{
    [Fact(DisplayName =
        "R2-F1 803 FULL round 2: successful open after deadline cannot start SQL")]
    public async Task Late_successful_open_returns_busy_and_releases_resources()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        await using var source = new LateSuccessfulSource(db.Context.DataSource);
        await using var context = new OrmContext(source, db.Context.Provider);
        db.Sink.Clear();

        var budget = TimeSpan.FromMilliseconds(250);
        var limit = budget + SqliteMaintenanceDatabase.Margin;
        var clock = Stopwatch.StartNew();
        var pending = context.Maintenance.CheckpointWalAsync(
            new WalCheckpointRequest(WalCheckpointMode.Truncate)
            {
                Timeout = budget
            });

        WalCheckpointResult result;
        try
        {
            result = await pending.WaitAsync(limit);
            clock.Stop();
        }
        finally
        {
            // Drain even when the watchdog or an assertion fails.
            await Record.ExceptionAsync(async () => { await pending; });
        }

        Assert.True(clock.Elapsed < limit, "Elapsed: " + clock.Elapsed);
        await source.Closed.Task.WaitAsync(SqliteMaintenanceDatabase.Margin);

        // Resource recovery must work even when the result assertion fails.
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Null(result.WalFrames);
        Assert.Null(result.CheckpointedFrames);
        Assert.Empty(db.Sink.Maintenance);
    }

    private sealed class LateSuccessfulSource(DbDataSource inner) : DbDataSource
    {
        internal TaskCompletionSource Closed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            throw new NotSupportedException();

        protected override async ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            var connection =
                await inner.OpenConnectionAsync(cancellationToken);

            connection.StateChange += (_, change) =>
            {
                if (change.CurrentState == ConnectionState.Closed)
                    Closed.TrySetResult();
            };

            try
            {
                var deadline = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = cancellationToken.Register(
                    () => deadline.TrySetResult());

                // Model successful open completion winning against
                // cancellation. Cancellation does not mandate a failed open.
                await deadline.Task.WaitAsync(TimeSpan.FromSeconds(5));

                // CancelAfter rounds to milliseconds; ensure the actual
                // budget, not merely its timer token, has expired.
                await Task.Delay(TimeSpan.FromMilliseconds(50));
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }
}
