using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

// The shared pipeline's edges that the AC tests do not reach: the budget's clamp and exhaustion, the UnitOfWork flow
// mark on a failed begin, a checkpoint that answers no row, and an unnamed in-memory database with no writer key.
public sealed class WalPipelineCoverageTests
{
    [Fact(DisplayName = "ORML-137 a budget longer than int.MaxValue milliseconds is held to it, and so is its command timeout")]
    public void A_long_budget_is_clamped()
    {
        var budget = new MaintenanceBudget(TimeSpan.FromDays(30));

        Assert.Equal(TimeSpan.FromMilliseconds(int.MaxValue), budget.Remaining);
        Assert.Equal(2_147_484, budget.CommandSeconds(0));
        Assert.Equal(2_147_485, budget.CommandSeconds(1));
    }

    [Fact(DisplayName = "ORML-137 a spent budget has nothing remaining")]
    public async Task A_spent_budget_is_zero()
    {
        var budget = new MaintenanceBudget(TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);

        Assert.Equal(TimeSpan.Zero, budget.Remaining);
    }

    [Fact(DisplayName = "ORML-137 a UnitOfWork whose begin fails closes the flow mark it installed")]
    public async Task A_failed_begin_closes_its_mark()
    {
        await using var context = new OrmContext(new RefusingSource(), SqliteProvider.Instance);

        await Assert.ThrowsAsync<SqliteException>(() => UnitOfWork.BeginAsync(context));

        // The mark BeginAsync installed in this flow is closed, so the next one has no live predecessor.
        var probe = WriterFlow.Enter();
        Assert.Null(probe.Previous);
        probe.Close();
    }

    [Fact(DisplayName = "ORML-137 a UnitOfWork whose writer key cannot be read is disposed and its mark closed")]
    public async Task A_failed_writer_key_disposes_the_unit()
    {
        await using var db = await WalFixture.WithItemsAsync(0);
        await using var context = new OrmContext(db.Context.DataSource, new KeyFailingProvider());

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => UnitOfWork.BeginAsync(context));
        Assert.Equal(KeyFailingProvider.Message, failure.Message);

        var probe = WriterFlow.Enter();
        Assert.Null(probe.Previous);
        probe.Close();

        // The unit was disposed: its writer gate is free for the next write.
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
    }

    [Fact(DisplayName = "ORML-137 a checkpoint statement that answers no row is a provider defect, not a status")]
    public async Task A_rowless_checkpoint_throws()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        var (context, _) = db.Counting(new TunedSqliteProvider(SqliteProvider.Instance.Capabilities) { CheckpointText = "SELECT 1 WHERE 0" });

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Passive, 5000)));

        Assert.Contains("returned no row", failure.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORML-137 an unnamed in-memory database has no writer key: the lease holds nothing and the call runs")]
    public async Task An_unnamed_memory_database_runs_without_a_gate()
    {
        SqliteProviderFactory.Register();
        await using var context = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", ConnectionString = "Data Source=:memory:" });

        Assert.Null(SqliteProvider.Instance.WriterKeyOf(context.DataSource));
        var lease = await SqliteProvider.Instance.TryEnterWriteAsync(context.DataSource, TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.True(lease.Entered);
        Assert.Null(lease.Release);

        var result = await context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000));

        Assert.Equal(MaintenanceStatus.NotWal, result.Status);
    }

    [Fact(DisplayName = "ORML-137 D12.2/D12.4 a cancellation during the open throws OperationCanceledException carrying the caller's token")]
    public async Task A_cancelled_open_is_cancellation()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        await using var source = new ScriptedOpenSource(db.Context.DataSource);
        await using var context = new OrmContext(source, db.Context.Provider);
        using var cancel = new CancellationTokenSource();
        source.OnOpen = async token =>
        {
            await cancel.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
        };

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000), cancel.Token));

        Assert.Equal(cancel.Token, failure.CancellationToken);
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
    }

    [Fact(DisplayName = "ORML-137 D12.2 an open that fails with contention after the caller cancelled is cancellation, with the failure inside")]
    public async Task Contention_after_cancellation_is_cancellation()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        await using var source = new ScriptedOpenSource(db.Context.DataSource);
        await using var context = new OrmContext(source, db.Context.Provider);
        using var cancel = new CancellationTokenSource();
        var busy = new SqliteException("busy while opening", 5);
        source.OnOpen = async _ =>
        {
            await cancel.CancelAsync();
            throw busy;
        };

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 5000), cancel.Token));

        Assert.Equal(cancel.Token, failure.CancellationToken);
        Assert.Same(busy, failure.InnerException);
    }

    [Fact(DisplayName = "ORML-137 D12.2 an open failure that is not contention, or a cancellation the budget did not cause, propagates unchanged")]
    public async Task Other_open_failures_propagate()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        await using var source = new ScriptedOpenSource(db.Context.DataSource);
        await using var context = new OrmContext(source, db.Context.Provider);
        var refused = new SqliteException("cannot open", 14);
        var own = new OperationCanceledException("the source gave up on its own");

        source.OnOpen = _ => throw refused;
        Assert.Same(refused, await Assert.ThrowsAsync<SqliteException>(
            () => context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Passive, 5000))));

        source.OnOpen = _ => throw own;
        Assert.Same(own, await Assert.ThrowsAsync<OperationCanceledException>(
            () => context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Passive, 5000))));

        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
    }

    [Fact(DisplayName = "ORML-137 a UnitOfWork on a provider without the maintenance port marks no writer key")]
    public async Task A_unit_without_the_port_has_no_key()
    {
        await using var db = await WalFixture.WithItemsAsync(0);
        await using var context = new OrmContext(db.Context.DataSource, new PortlessProvider());

        await using (var unit = await UnitOfWork.BeginAsync(context))
        {
            // The unit's mark carries no key, so maintenance in this flow on the same file is not refused by it.
            Assert.False(WriterFlow.Holds(SqliteProvider.Instance.WriterKeyOf(db.Context.DataSource)!));
            await unit.CommitAsync();
        }
    }

    // Opens through the wrapped source unless OnOpen says otherwise.
    private sealed class ScriptedOpenSource(DbDataSource inner) : DbDataSource
    {
        internal Func<CancellationToken, Task>? OnOpen { get; set; }

        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() => inner.CreateConnection();

        protected override async ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (OnOpen is { } script) await script(cancellationToken);
            return await inner.OpenConnectionAsync(cancellationToken);
        }
    }

    private sealed class RefusingSource : DbDataSource
    {
        public override string ConnectionString => "Data Source=refused.db";

        protected override DbConnection CreateDbConnection() => throw new SqliteException("the source refuses to open", 14);

        protected override ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromException<DbConnection>(new SqliteException("the source refuses to open", 14));
    }

    // SQLite in every respect except that reading the writer key fails, after the transaction has begun.
    private sealed class KeyFailingProvider : ISqlProvider, IMaintenanceProvider
    {
        internal const string Message = "the writer key cannot be read";

        public string Id => "key-failing";

        public CapabilitySet Capabilities => SqliteProvider.Instance.Capabilities;

        public ISerializer CreateSerializer() => SqliteProvider.Instance.CreateSerializer();

        public TimeSpan MaintenanceTimeout => SqliteProvider.Instance.MaintenanceTimeout;

        public string? WriterKeyOf(DbDataSource source) => throw new InvalidOperationException(Message);

        public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
            SqliteProvider.Instance.TryEnterWriteAsync(source, timeout, ct);
    }
}
