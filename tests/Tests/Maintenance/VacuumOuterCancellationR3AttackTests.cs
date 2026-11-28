// §803 provenance: item ORML-134, pass FULL, round 3. Reviewer-authored proposal, placed unchanged (QA screening run, not committed).
using System.Data;
using System.Data.Common;
using System.Threading.Channels;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class VacuumOuterCancellationR3AttackTests
{
    private static readonly TimeSpan Limit =
        TimeSpan.FromSeconds(5) + SqliteMaintenanceDatabase.Margin;

    [Theory(DisplayName =
        "R3-F1 803 FULL round 3: cancellation before VACUUM's outer continuation wins")]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Cancellation_before_outer_continuation_wins(
        bool checkpoint, int busy)
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        using var cancellation = new CancellationTokenSource();

        var capabilities = SqliteProvider.Instance.Capabilities;
        if (!checkpoint)
            capabilities = capabilities.With(
                Capability.WalCheckpoint, Support.None);

        var provider = new TunedSqliteProvider(capabilities)
        {
            CheckpointText = busy == 0
                ? "SELECT 0, 7, 3"
                : "SELECT 1, 7, 3",
        };

        await using var source =
            new DeferredOpenSource(db.Context.DataSource);
        await using var context = new OrmContext(source, provider);
        var queue = new ContinuationQueue();
        db.Sink.Clear();

        Task<VacuumResult> pending;
        var previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(queue);
            pending = context.Maintenance.VacuumAsync(
                VacuumFixture.Plain(5000), cancellation.Token);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Exception? failure;
        try
        {
            var connection = await source.Opened.Task.WaitAsync(Limit);
            source.Release.TrySetResult();

            // Finish native execution and connection cleanup.
            while (connection.State == ConnectionState.Open &&
                   !pending.IsCompleted)
            {
                await queue.RunNextAsync();
            }

            // Let the inner runner finish with the token still live.
            // The outer VACUUM continuation remains queued.
            if (!pending.IsCompleted)
                await queue.RunNextAsync();

            cancellation.Cancel();
            failure = await DrainAsync(pending, queue);

            Assert.NotEqual(ConnectionState.Open, connection.State);
        }
        finally
        {
            source.Release.TrySetResult();
            cancellation.Cancel();
            await DrainAsync(pending, queue);
        }

        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);

        Assert.Equal(
            checkpoint
                ? MaintenanceOperation.WalCheckpoint
                : MaintenanceOperation.Vacuum,
            Assert.Single(db.Sink.Maintenance).Operation);

        var cancelled =
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(cancellation.Token, cancelled.CancellationToken);
    }

    private static async Task<Exception?> DrainAsync(
        Task<VacuumResult> pending, ContinuationQueue queue)
    {
        while (!pending.IsCompleted)
            await queue.RunNextAsync();

        return await Record.ExceptionAsync(async () =>
        {
            await pending;
        });
    }

    private sealed class DeferredOpenSource(DbDataSource inner)
        : DbDataSource
    {
        internal TaskCompletionSource<DbConnection> Opened { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            throw new NotSupportedException();

        protected override async ValueTask<DbConnection>
            OpenDbConnectionAsync(
                CancellationToken cancellationToken = default)
        {
            DbConnection connection;
            try
            {
                connection = await inner
                    .OpenConnectionAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception failure)
            {
                Opened.TrySetException(failure);
                throw;
            }

            Opened.TrySetResult(connection);
            await Release.Task.ConfigureAwait(false);
            return connection;
        }
    }

    private sealed class ContinuationQueue : SynchronizationContext
    {
        private readonly Channel<(
            SendOrPostCallback Callback, object? State)> _callbacks =
            Channel.CreateUnbounded<(
                SendOrPostCallback Callback, object? State)>();

        public override void Post(SendOrPostCallback callback, object? state)
        {
            if (!_callbacks.Writer.TryWrite((callback, state)))
                throw new InvalidOperationException("Continuation queue closed.");
        }

        internal async Task RunNextAsync()
        {
            var work = await _callbacks.Reader
                .ReadAsync().AsTask().WaitAsync(Limit);

            // Prevent captured continuations from inlining into this turn.
            var previous = Current;
            try
            {
                SetSynchronizationContext(null);
                work.Callback(work.State);
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }
    }
}
