// §803 provenance: item ORML-134, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class VacuumOpenContentionAttackTests
{
    [Theory(DisplayName =
        "F6 803 FULL round 1: native contention during connection opening returns Busy")]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Open_contention_is_returned_and_releases_the_gate(int code)
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await db.ExecAsync(WalFixture.Items);
        db.Sink.Clear();

        await using var source = new ContendedOpenSource(
            db.Context.DataSource,
            new SqliteException("contention while opening", code));
        await using var context =
            new OrmContext(source, SqliteProvider.Instance);

        var result = await context.Maintenance.VacuumAsync(
            VacuumFixture.Plain(200));

        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Empty(db.Sink.Maintenance);

        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
    }

    private sealed class ContendedOpenSource(
        DbDataSource inner,
        DbException failure) : DbDataSource
    {
        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() => throw failure;

        protected override ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromException<DbConnection>(failure);
        }
    }
}
