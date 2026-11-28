using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

// ADR e8c50bcc v5 D12.3 (ORML-135 R2-F2, TL fix list 6b33e96e item 2): a guard setup that spends the whole budget
// starts no ANALYZE. The call answers Busy, reports no statement, leaves no statistics, and releases its connection and
// the writer gate. The DEV regression test for the property AnalyzeSetupBudgetR2AttackTests targets while it is HELD.
public sealed class AnalyzeSetupBudgetTests
{
    [Fact(DisplayName = "ORML-135 D12.3 a guard setup that spends the budget starts no ANALYZE: Busy, no event, no statistics")]
    public async Task A_spent_setup_starts_no_analyze()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await db.ExecAsync(WalFixture.Items);
        var budget = TimeSpan.FromMilliseconds(300);
        await using var source = new SlowSetupSource(db.Context.DataSource, budget + TimeSpan.FromMilliseconds(200));
        var provider = new TunedSqliteProvider(SqliteProvider.Instance.Capabilities)
        {
            Guard = new MaintenanceSessionGuard("SELECT spend_the_budget()", "SELECT 1"),
        };
        await using var context = new OrmContext(source, provider);
        db.Sink.Clear();

        var result = await context.Maintenance.AnalyzeAsync(
            new AnalyzeRequest(AnalyzeScope.Table) { Table = AnalyzeFixture.A, Timeout = budget });

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Equal(1, source.SetupCalls);
        Assert.Empty(db.Sink.Maintenance);
        Assert.Equal(0L, await db.LongAsync("SELECT count(*) FROM sqlite_schema WHERE name = 'sqlite_stat1'"));

        // The writer gate and the connection were released: a write and the next ANALYZE proceed.
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
        Assert.Equal(MaintenanceStatus.Completed, (await db.Maintenance.AnalyzeAsync(AnalyzeFixture.Table(AnalyzeFixture.A, 5000))).Status);
    }

    // Registers spend_the_budget() on every connection it opens: the guard setup calls it and it sleeps past the budget.
    private sealed class SlowSetupSource(DbDataSource inner, TimeSpan delay) : DbDataSource
    {
        private int _setupCalls;

        internal int SetupCalls => Volatile.Read(ref _setupCalls);

        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() => throw new NotSupportedException();

        protected override async ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = await inner.OpenConnectionAsync(cancellationToken);
            Assert.IsAssignableFrom<SqliteConnection>(connection).CreateFunction("spend_the_budget", () =>
            {
                Interlocked.Increment(ref _setupCalls);
                Thread.Sleep(delay);
                return 0;
            });
            return connection;
        }
    }
}
