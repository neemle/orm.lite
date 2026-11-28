// §803 provenance: item ORML-135, pass FULL, round 3. Reviewer-authored proposal, placed unchanged (QA screening run, not committed).
using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

// Reviewer-authored replacement for AnalyzeSetupBudgetR2AttackTests.
// R3-F2 is a replacement attempt, not a new finding.
public sealed class AnalyzeSetupBudgetR3AttackTests
{
    [Fact(DisplayName =
        "R3-F2 — 803 FULL round 3 — exhausted setup starts no ANALYZE and releases resources")]
    public async Task Exhausted_setup_budget_does_not_start_analyze()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await db.ExecAsync(WalFixture.Items);

        const string statisticsExist =
            "SELECT count(*) FROM sqlite_schema WHERE name = 'sqlite_stat1'";

        Assert.Equal(0L, await db.LongAsync(statisticsExist));

        var timeout = TimeSpan.FromSeconds(1);
        var setupDelay = timeout + TimeSpan.FromMilliseconds(250);

        await using var source =
            new SetupDelaySource(db.Context.DataSource, setupDelay);

        var provider = new TunedSqliteProvider(
            SqliteProvider.Instance.Capabilities)
        {
            Guard = new MaintenanceSessionGuard(
                "SELECT r3_spend_budget()",
                "SELECT r3_teardown()")
        };

        await using var context = new OrmContext(source, provider);
        db.Sink.Clear();

        var clock = Stopwatch.StartNew();
        var result = await context.Maintenance.AnalyzeAsync(
            new AnalyzeRequest(AnalyzeScope.Table)
            {
                Table = AnalyzeFixture.A,
                Timeout = timeout
            });
        clock.Stop();

        // Establish that the intended setup path actually ran.
        Assert.Equal(1, source.SetupCalls);

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.False(result.IsComplete);
        Assert.Empty(db.Sink.Maintenance);
        Assert.Equal(0L, await db.LongAsync(statisticsExist));

        // Check cleanup before disposing the context or source ourselves.
        Assert.Equal(1, source.TeardownCalls);
        var opened = Assert.IsAssignableFrom<SqliteConnection>(
            source.Opened);
        Assert.Equal(ConnectionState.Closed, opened.State);

        Assert.True(
            clock.Elapsed <= timeout + SqliteMaintenanceDatabase.Margin,
            $"Call took {clock.Elapsed}; budget was {timeout}, " +
            $"margin was {SqliteMaintenanceDatabase.Margin}.");

        // These use the original context and the same database writer gate.
        var recoveryLimit =
            TimeSpan.FromSeconds(5) + SqliteMaintenanceDatabase.Margin;

        await db.WriteAsync().WaitAsync(recoveryLimit);

        var next = await db.Maintenance.AnalyzeAsync(
            AnalyzeFixture.Table(AnalyzeFixture.A, 5000))
            .WaitAsync(recoveryLimit);

        Assert.Equal(MaintenanceStatus.Completed, next.Status);
        Assert.True(next.IsComplete);
        Assert.Equal(
            MaintenanceOperation.Analyze,
            Assert.Single(db.Sink.Maintenance).Operation);
    }

    private sealed class SetupDelaySource(
        DbDataSource inner,
        TimeSpan delay) : DbDataSource
    {
        private int _setupCalls;
        private int _teardownCalls;

        internal int SetupCalls => Volatile.Read(ref _setupCalls);
        internal int TeardownCalls => Volatile.Read(ref _teardownCalls);
        internal DbConnection? Opened { get; private set; }

        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            throw new NotSupportedException();

        protected override async ValueTask<DbConnection>
            OpenDbConnectionAsync(
                CancellationToken cancellationToken = default)
        {
            var connection =
                await inner.OpenConnectionAsync(cancellationToken);

            try
            {
                // The delivered source returns OrmSqliteConnection,
                // an internal subclass of SqliteConnection.
                var sqlite =
                    Assert.IsAssignableFrom<SqliteConnection>(connection);

                sqlite.CreateFunction("r3_spend_budget", () =>
                {
                    Interlocked.Increment(ref _setupCalls);
                    Thread.Sleep(delay);
                    return 0;
                });

                sqlite.CreateFunction("r3_teardown", () =>
                {
                    Interlocked.Increment(ref _teardownCalls);
                    return 0;
                });

                Opened = connection;
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        protected override async ValueTask DisposeAsyncCore()
        {
            // Clean up even if an assertion discovers a product leak.
            // All lifecycle assertions happen before this fallback.
            if (Opened is { } connection)
                await connection.DisposeAsync();

            await base.DisposeAsyncCore();
        }
    }
}
