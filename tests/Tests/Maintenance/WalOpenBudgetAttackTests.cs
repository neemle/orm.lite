// §803 provenance: item ORML-137, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
using System.Data.Common;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class WalOpenBudgetAttackTests
{
    [Fact(DisplayName =
        "F2 803 FULL round 1: opening spends the maintenance budget")]
    public async Task Stalled_open_must_release_the_writer_gate_by_the_deadline()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        await using var source = new PausedOpenDataSource(db.Context.DataSource);
        await using var context = new OrmContext(source, db.Context.Provider);

        var checkpoint = context.Maintenance.CheckpointWalAsync(
            WalFixture.Request(WalCheckpointMode.Truncate, 100));

        // Observe any permitted deadline outcome without prescribing its type.
        var observed = Record.ExceptionAsync(async () =>
        {
            await checkpoint;
        });

        Task? write = null;
        var finishedWithinMargin = false;

        try
        {
            await source.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

            write = Task.Run(() => db.WriteAsync());
            var finished = Task.WhenAll(observed, write);
            var deadline = Task.Delay(
                TimeSpan.FromMilliseconds(100)
                + SqliteMaintenanceDatabase.Margin);

            await Task.WhenAny(finished, deadline);
            finishedWithinMargin = finished.IsCompleted;
        }
        finally
        {
            // Always unblock and drain the operation, including on assertion
            // failure. No database work is left running after this test.
            source.AllowOpen.TrySetResult();
            await observed.WaitAsync(TimeSpan.FromSeconds(5));

            if (write is not null)
                await write.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.True(
            finishedWithinMargin,
            "The 100 ms maintenance call or its queued ORM write remained "
            + "blocked beyond the budget plus the fixture's 2 s margin.");
    }

    private sealed class PausedOpenDataSource(DbDataSource inner) : DbDataSource
    {
        internal TaskCompletionSource Entered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource AllowOpen { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            inner.CreateConnection();

        protected override async ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await AllowOpen.Task.WaitAsync(cancellationToken);
            return await inner.OpenConnectionAsync(cancellationToken);
        }
    }
}
