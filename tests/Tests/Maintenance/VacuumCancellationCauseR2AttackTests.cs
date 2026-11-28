// §803 provenance: item ORML-134, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class VacuumCancellationCauseR2AttackTests
{
    [Fact(DisplayName =
        "R2-F2 803 FULL round 2: cancellation during teardown preserves contention")]
    public async Task Cancellation_retains_the_original_contention_exception()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync(WalFixture.Items);

        await using var blocker = await db.RawAsync();
        await using var transaction = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(
            blocker,
            "INSERT INTO items (v) VALUES ('blocker')",
            transaction);

        using var cancellation = new CancellationTokenSource();

        // Exercise VACUUM's shared guarded pipeline without a prerequisite
        // checkpoint consuming the contention first.
        var provider = new TunedSqliteProvider(
            SqliteProvider.Instance.Capabilities.With(
                Capability.WalCheckpoint, Support.None))
        {
            Guard = new MaintenanceSessionGuard(
                "SELECT 1",
                "SELECT r2_cancel_during_teardown()"),
        };

        await using var source = new CancellingTeardownSource(
            db.Context.DataSource, cancellation);
        await using var context = new OrmContext(source, provider);
        db.Sink.Clear();

        Exception? failure;
        try
        {
            failure = await Record.ExceptionAsync(async () =>
            {
                await context.Maintenance.VacuumAsync(
                    VacuumFixture.Plain(200), cancellation.Token);
            });
        }
        finally
        {
            await transaction.RollbackAsync();
        }

        // The writer gate must also have been released.
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);

        Assert.True(cancellation.IsCancellationRequested);

        var statement = Assert.Single(db.Sink.Maintenance);
        Assert.Equal(MaintenanceOperation.Vacuum, statement.Operation);

        var original = Assert.IsType<SqliteException>(statement.Error);
        Assert.True(SqliteProvider.Instance.IsContention(original));

        var cancelled =
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(cancellation.Token, cancelled.CancellationToken);
        Assert.Same(original, cancelled.InnerException);
    }

    private sealed class CancellingTeardownSource(
        DbDataSource inner,
        CancellationTokenSource cancellation) : DbDataSource
    {
        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            inner.CreateConnection();

        protected override async ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            var connection =
                await inner.OpenConnectionAsync(cancellationToken);

            try
            {
                ((SqliteConnection)connection).CreateFunction(
                    "r2_cancel_during_teardown",
                    () =>
                    {
                        cancellation.Cancel();
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
