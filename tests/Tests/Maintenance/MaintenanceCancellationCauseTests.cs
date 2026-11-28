using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

// ADR e8c50bcc v5 D12.4(a) (TL fix list 6b33e96e item 6, M135-R2c): once the caller's token is cancelled the call
// throws OperationCanceledException carrying it, with the original exception as its inner exception, on both ways a
// cancelled run ends: a failure that propagates out of the run, and a Busy whose contention was caught while the token
// was still live. Contention is classified only while the token is live: a contention failure that lands after the
// cancellation is not consulted as one, it propagates as the inner exception.
public sealed class MaintenanceCancellationCauseTests
{
    [Fact(DisplayName = "ORML-135 D12.4(a) a non-contention failure that lands after cancellation is the inner exception")]
    public async Task A_failure_after_cancellation_is_the_inner_exception()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        using var cancellation = new CancellationTokenSource();
        var probe = Probe(db, cancellation, "SELECT cancel_then_fail(1)", guard: null);
        await using var source = probe.Source;
        await using var context = probe.Context;
        var port = probe.Port;

        var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000), cancellation.Token));

        Assert.Equal(cancellation.Token, cancelled.CancellationToken);
        var inner = Assert.IsType<SqliteException>(cancelled.InnerException);
        Assert.Equal(1, inner.SqliteErrorCode);
        Assert.Contains(CauseSource.Probe, inner.Message, StringComparison.Ordinal);
        Assert.Equal(0, port.ClassifiedAfterCancel);
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
    }

    [Fact(DisplayName = "ORML-135 D12.4(a) a Busy's contention is the inner exception when cancellation lands after it was caught")]
    public async Task A_busy_cause_is_the_inner_exception()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        using var cancellation = new CancellationTokenSource();
        // The statement meets contention while the token is live (Busy, with that cause); the guard's teardown cancels.
        var probe = Probe(db, cancellation, "SELECT fail_with(5)",
            new MaintenanceSessionGuard("SELECT 1", "SELECT cancel_now()"));
        await using var source = probe.Source;
        await using var context = probe.Context;
        var port = probe.Port;

        var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000), cancellation.Token));

        Assert.Equal(cancellation.Token, cancelled.CancellationToken);
        var inner = Assert.IsType<SqliteException>(cancelled.InnerException);
        Assert.Equal(5, inner.SqliteErrorCode);
        Assert.True(port.ClassifiedLive > 0);
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
    }

    [Fact(DisplayName = "ORML-135 D12.4(a) a contention that lands after cancellation is not classified: it is the inner exception")]
    public async Task A_contention_after_cancellation_is_not_classified()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        using var cancellation = new CancellationTokenSource();
        var probe = Probe(db, cancellation, "SELECT cancel_then_fail(5)", guard: null);
        await using var source = probe.Source;
        await using var context = probe.Context;
        var port = probe.Port;

        var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000), cancellation.Token));

        Assert.Equal(cancellation.Token, cancelled.CancellationToken);
        var inner = Assert.IsType<SqliteException>(cancelled.InnerException);
        Assert.Equal(5, inner.SqliteErrorCode);
        Assert.Equal(0, port.ClassifiedAfterCancel);
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
    }

    private static (CauseSource Source, OrmContext Context, CountingPort Port) Probe(
        SqliteMaintenanceDatabase db, CancellationTokenSource cancellation, string checkpoint, MaintenanceSessionGuard? guard)
    {
        var port = new CountingPort(cancellation, checkpoint, guard);
        var source = new CauseSource(db.Context.DataSource, cancellation);
        return (source, new OrmContext(source, port), port);
    }

    // SQLite's port with a replaced checkpoint text and guard; it counts IsContention calls by whether the token was
    // already cancelled when the core asked.
    private sealed class CountingPort(CancellationTokenSource cancellation, string checkpoint, MaintenanceSessionGuard? guard)
        : ISqlProvider, IMaintenanceProvider
    {
        private static readonly SqliteProvider Inner = SqliteProvider.Instance;
        private int _live;
        private int _afterCancel;

        internal int ClassifiedLive => Volatile.Read(ref _live);

        internal int ClassifiedAfterCancel => Volatile.Read(ref _afterCancel);

        public string Id => "sqlite-cause-probe";
        public CapabilitySet Capabilities => Inner.Capabilities;
        public ISerializer CreateSerializer() => Inner.CreateSerializer();
        public TimeSpan MaintenanceTimeout => Inner.MaintenanceTimeout;
        public bool HasWriterGate => Inner.HasWriterGate;
        public string? WriterKeyOf(DbDataSource source) => Inner.WriterKeyOf(source);

        public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
            Inner.TryEnterWriteAsync(source, timeout, ct);

        public MaintenanceSessionGuard? RenderLockGuard(TimeSpan remaining) => guard;

        public string RenderWalCheckpoint(WalCheckpointMode mode) => checkpoint;

        public bool IsContention(DbException failure)
        {
            if (cancellation.IsCancellationRequested) Interlocked.Increment(ref _afterCancel);
            else Interlocked.Increment(ref _live);
            return Inner.IsContention(failure);
        }
    }

    // Opens through the test database's source and registers the probe functions on every connection.
    private sealed class CauseSource(DbDataSource inner, CancellationTokenSource cancellation) : DbDataSource
    {
        internal const string Probe = "cause probe";

        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() => throw new NotSupportedException();

        protected override async ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = await inner.OpenConnectionAsync(cancellationToken);
            var sqlite = Assert.IsAssignableFrom<SqliteConnection>(connection);
            sqlite.CreateFunction("cancel_now", () =>
            {
                cancellation.Cancel();
                return 0L;
            });
            sqlite.CreateFunction<long, long>("fail_with", code => throw new SqliteException(Probe, (int)code));
            sqlite.CreateFunction<long, long>("cancel_then_fail", code =>
            {
                cancellation.Cancel();
                throw new SqliteException(Probe, (int)code);
            });
            return connection;
        }
    }
}
