// §803 provenance: item ORML-137, pass FULL, round 3. Reviewer-authored proposal, placed unchanged (QA screening run, not committed).
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class WalRetryClassificationCancellationR3AttackTests
{
    [Fact(DisplayName =
        "R3-F1 803 FULL round 3: cancellation during retry classification retains native contention")]
    public async Task Cancellation_keeps_the_exception_being_classified()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        using var cancellation = new CancellationTokenSource();
        var provider = new CancellingRetryProvider(cancellation);

        await using var source = new ContentionSource(db.Context.DataSource);
        await using var context = new OrmContext(source, provider);
        db.Sink.Clear();

        // SQLite's async command API may execute synchronously.
        var pending = Task.Run(() =>
            context.Maintenance.CheckpointWalAsync(
                WalFixture.Request(WalCheckpointMode.Truncate, 10_000),
                cancellation.Token));

        Exception? observed;
        try
        {
            observed = await Record.ExceptionAsync(() =>
                pending.WaitAsync(
                    TimeSpan.FromSeconds(10)
                    + SqliteMaintenanceDatabase.Margin));
        }
        finally
        {
            cancellation.Cancel();
            await Record.ExceptionAsync(async () => { await pending; });
        }

        // Prove that cancellation was injected while classifying an actual
        // SQLite contention exception, after statement execution began.
        var original =
            Assert.IsType<SqliteException>(provider.ClassifiedFailure);
        Assert.Equal(5, original.SqliteErrorCode & 0xFF);

        var cancelled =
            Assert.IsAssignableFrom<OperationCanceledException>(observed);
        Assert.Equal(cancellation.Token, cancelled.CancellationToken);

        Assert.NotNull(source.Opened);
        Assert.Equal(ConnectionState.Closed, source.Opened!.State);

        var statement = Assert.Single(db.Sink.Maintenance);
        Assert.Equal(
            MaintenanceOperation.WalCheckpoint,
            statement.Operation);

        // Check recovery before the assertion expected to fail at this head.
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
        var next = await db.Maintenance.CheckpointWalAsync(
                WalFixture.Request(WalCheckpointMode.Truncate, 5000))
            .WaitAsync(
                TimeSpan.FromSeconds(5)
                + SqliteMaintenanceDatabase.Margin);
        Assert.Equal(MaintenanceStatus.Completed, next.Status);

        // D12.4(a): preserve the original exception, not merely its message.
        Assert.Same(original, cancelled.InnerException);
    }

    private sealed class CancellingRetryProvider(
        CancellationTokenSource cancellation)
        : ISqlProvider, IMaintenanceProvider
    {
        private static readonly SqliteProvider Inner =
            SqliteProvider.Instance;

        internal DbException? ClassifiedFailure { get; private set; }

        public string Id => "sqlite-r3-retry-cancellation";
        public CapabilitySet Capabilities => Inner.Capabilities;
        public ISerializer CreateSerializer() => Inner.CreateSerializer();
        public TimeSpan MaintenanceTimeout => Inner.MaintenanceTimeout;
        public bool HasWriterGate => Inner.HasWriterGate;
        public TimeSpan? ContentionRetrySlice => Inner.ContentionRetrySlice;

        public string? WriterKeyOf(DbDataSource source) =>
            Inner.WriterKeyOf(source);

        public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(
            DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
            Inner.TryEnterWriteAsync(source, timeout, ct);

        public string RenderWalCheckpoint(WalCheckpointMode mode) =>
            "SELECT r3_contention(5)";

        public bool IsContention(DbException failure)
        {
            if (!Inner.IsContention(failure))
                return false;

            ClassifiedFailure ??= failure;

            // Deterministically place cancellation after the retry helper's
            // initial token check but before classification returns.
            cancellation.Cancel();
            return true;
        }
    }

    private sealed class ContentionSource(DbDataSource inner) : DbDataSource
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
            try
            {
                var sqlite =
                    Assert.IsAssignableFrom<SqliteConnection>(connection);
                sqlite.CreateFunction<long, long>(
                    "r3_contention",
                    code => throw new SqliteException(
                        "R3 contention classification probe", (int)code));

                Opened = connection;
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
