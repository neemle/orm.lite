// §803 provenance: item ORML-138, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
// HELD (§803.8): admitted FAILS, routed by NINE-FIVE-ROUTING 7cd5d189 on ORML-138 to fix list item 2, the late open
// (TL fix list 6b33e96e on ORML-137). The Skip below is the HELD marker; the commit that turns it green removes it.
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class IntegrityLateOpenR2AttackTests
{
    [Fact(Skip = "HELD: routing 7cd5d189 on ORML-138 and ORML-137 (fix list 6b33e96e item 2)", DisplayName =
        "R2-F1 803 FULL round 2: a successful open after expiry starts no integrity statement")]
    public async Task Expired_open_returns_busy_without_sql()
    {
        await using var db =
            await SqliteMaintenanceDatabase.OpenRollbackJournalAsync();
        await using var source = new LateOpenSource(db.Context.DataSource);
        await using var context = new OrmContext(source, db.Context.Provider);
        var timeout = TimeSpan.FromMilliseconds(200);
        db.Sink.Clear();

        var clock = Stopwatch.StartNew();
        var result = await context.Maintenance.CheckIntegrityAsync(
            new IntegrityCheckRequest(IntegrityCheckMode.Full)
            {
                Timeout = timeout
            }).WaitAsync(timeout + SqliteMaintenanceDatabase.Margin);
        clock.Stop();

        Assert.NotNull(source.Opened);
        Assert.Equal(ConnectionState.Closed, source.Opened!.State);
        Assert.True(
            clock.Elapsed < timeout + SqliteMaintenanceDatabase.Margin,
            "Elapsed: " + clock.Elapsed);

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Empty(db.Sink.Maintenance);

        Assert.Equal(
            MaintenanceStatus.Completed,
            (await db.Maintenance.CheckIntegrityAsync(
                new IntegrityCheckRequest(IntegrityCheckMode.Full))).Status);
    }

    private sealed class LateOpenSource(DbDataSource inner) : DbDataSource
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
            Opened = connection;

            try
            {
                // The handle has opened successfully. Deliver that success
                // after cancellation wins the deadline race.
                try
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                }

                // Avoid depending on timer rounding at the budget boundary.
                await Task.Delay(TimeSpan.FromMilliseconds(25));
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
