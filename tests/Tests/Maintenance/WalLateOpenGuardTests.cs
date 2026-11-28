using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

// ADR e8c50bcc v5 D12.2/D12.3 (TL fix list 6b33e96e item 2): an open that succeeds only after the budget's timer fired
// starts nothing on the connection, not even the lock guard's setup, which would otherwise be rendered from a spent
// budget (on PostgreSQL `SET lock_timeout = '0ms'`, which means no limit at all).
public sealed class WalLateOpenGuardTests
{
    [Fact(DisplayName = "ORML-137 D12.3 an open that succeeds after the budget ran out runs no guard setup and no statement: Busy")]
    public async Task A_late_open_runs_no_guard_setup()
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        await using var source = new LateOpenSource(db.Context.DataSource);
        var provider = new TunedSqliteProvider(SqliteProvider.Instance.Capabilities)
        {
            Guard = new MaintenanceSessionGuard("SELECT count_setup()", "SELECT 1"),
        };
        await using var context = new OrmContext(source, provider);
        db.Sink.Clear();

        var result = await context.Maintenance.CheckpointWalAsync(WalFixture.Request(WalCheckpointMode.Truncate, 200))
            .WaitAsync(TimeSpan.FromMilliseconds(200) + SqliteMaintenanceDatabase.Margin);

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Equal(1, source.Opens);
        Assert.Equal(0, source.SetupCalls);
        Assert.Empty(db.Sink.Maintenance);
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
    }

    [Fact(DisplayName = "ORML-137 D12.2 the open timer's delay is the remaining budget rounded up to whole milliseconds, zero once spent")]
    public async Task The_timer_delay_never_undercuts_the_budget()
    {
        var budget = new MaintenanceBudget(TimeSpan.FromTicks(10_000 * 400 + 7));
        var delay = budget.TimerDelay;
        var after = budget.Remaining;

        Assert.Equal(0, delay.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.True(delay >= after, delay + " < " + after);
        Assert.True(delay <= TimeSpan.FromMilliseconds(401), delay.ToString());

        var spent = new MaintenanceBudget(TimeSpan.FromMilliseconds(1));
        await Task.Delay(20);
        Assert.Equal(TimeSpan.Zero, spent.TimerDelay);
    }

    // Opens for real, then holds the open until its token fires and a little longer, and returns the connection anyway:
    // the open succeeds after the budget ran out. Every connection counts calls of count_setup().
    private sealed class LateOpenSource(DbDataSource inner) : DbDataSource
    {
        private int _opens;
        private int _setupCalls;

        internal int Opens => Volatile.Read(ref _opens);

        internal int SetupCalls => Volatile.Read(ref _setupCalls);

        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() => throw new NotSupportedException();

        protected override async ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _opens);
            var connection = await inner.OpenConnectionAsync(CancellationToken.None);
            Assert.IsAssignableFrom<SqliteConnection>(connection).CreateFunction("count_setup", () => Interlocked.Increment(ref _setupCalls));
            var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using (cancellationToken.Register(() => fired.TrySetResult()))
                await fired.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            return connection;
        }
    }
}
