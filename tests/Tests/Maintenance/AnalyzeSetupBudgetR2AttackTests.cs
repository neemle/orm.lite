// §803 provenance: item ORML-135, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
// HELD (§803.8): UNRESOLVED per NINE-FIVE-ROUTING aa572379 on ORML-135: the test fails in its own fixture
// (Assert.IsType<SqliteConnection> against OrmSqliteConnection), which only a forbidden edit would change. A
// SYNTHETIC question on ground (d) MANUFACTURED CONDITION asks the reviewer for a replacement; the Skip below is the
// HELD marker and stays until that replacement lands. Fix list item 2 fixes the property; a DEV regression test pins it.
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class AnalyzeSetupBudgetR2AttackTests
{
    [Fact(Skip = "HELD: routing aa572379 on ORML-135 and ORML-137, UNRESOLVED, replacement requested from the reviewer", DisplayName =
        "R2-F2 — 803 FULL round 2 — setup exhausting the budget prevents ANALYZE")]
    public async Task Exhausted_setup_budget_does_not_start_analyze()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await db.ExecAsync(WalFixture.Items);

        Assert.Equal(0L, await db.LongAsync(
            "SELECT count(*) FROM sqlite_schema WHERE name = 'sqlite_stat1'"));

        var timeout = TimeSpan.FromMilliseconds(500);
        // Setup finishes normally but deliberately spends the entire budget.
        var setupDelay = timeout + TimeSpan.FromMilliseconds(250);

        await using var source =
            new SetupDelaySource(db.Context.DataSource, setupDelay);
        var provider = new TunedSqliteProvider(
            SqliteProvider.Instance.Capabilities)
        {
            Guard = new MaintenanceSessionGuard(
                "SELECT r2_spend_budget()", "SELECT 1")
        };
        await using var context = new OrmContext(source, provider);
        db.Sink.Clear();

        var result = await context.Maintenance.AnalyzeAsync(
            new AnalyzeRequest(AnalyzeScope.Table)
            {
                Table = AnalyzeFixture.A,
                Timeout = timeout
            });

        Assert.Equal(1, source.SetupCalls);
        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Empty(db.Sink.Maintenance);
        Assert.Equal(0L, await db.LongAsync(
            "SELECT count(*) FROM sqlite_schema WHERE name = 'sqlite_stat1'"));

        // The exhausted operation must still release its connection and gate.
        await db.WriteAsync().WaitAsync(
            TimeSpan.FromSeconds(5) + SqliteMaintenanceDatabase.Margin);
        var next = await db.Maintenance.AnalyzeAsync(
            AnalyzeFixture.Table(AnalyzeFixture.A, 5000));
        Assert.Equal(MaintenanceStatus.Completed, next.Status);
    }

    private sealed class SetupDelaySource(
        DbDataSource inner, TimeSpan delay) : DbDataSource
    {
        private int _setupCalls;

        internal int SetupCalls => Volatile.Read(ref _setupCalls);

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
                var sqlite = Assert.IsType<SqliteConnection>(connection);
                sqlite.CreateFunction("r2_spend_budget", () =>
                {
                    Interlocked.Increment(ref _setupCalls);
                    Thread.Sleep(delay);
                    return 0;
                });
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
