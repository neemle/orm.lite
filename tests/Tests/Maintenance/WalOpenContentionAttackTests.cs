// §803 provenance: item ORML-137, pass FULL, round 1. Reviewer-authored proposal, placed unchanged.
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class WalOpenContentionAttackTests
{
    [Theory(DisplayName =
        "F3 803 FULL round 1: opening contention returns Busy")]
    [InlineData(5, WalCheckpointMode.Passive)]
    [InlineData(6, WalCheckpointMode.Passive)]
    [InlineData(5, WalCheckpointMode.Truncate)]
    [InlineData(6, WalCheckpointMode.Truncate)]
    public async Task Opening_contention_must_be_a_result(
        int code,
        WalCheckpointMode mode)
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        await using var source = new FailingOpenDataSource(
            db.Context.DataSource,
            new SqliteException("opening met contention", code));
        await using var context = new OrmContext(source, db.Context.Provider);

        WalCheckpointResult? result = null;
        var error = await Record.ExceptionAsync(async () =>
        {
            result = await context.Maintenance.CheckpointWalAsync(
                WalFixture.Request(mode, 200));
        });

        // Also verify that the failed opening did not retain the writer gate.
        await db.WriteAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(MaintenanceStatus.Busy, result!.Status);
        Assert.Equal(mode, result.Mode);
        Assert.Null(result.WalFrames);
        Assert.Null(result.CheckpointedFrames);
        Assert.False(result.IsComplete);
    }

    private sealed class FailingOpenDataSource(
        DbDataSource inner,
        DbException failure) : DbDataSource
    {
        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            inner.CreateConnection();

        protected override ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<DbConnection>(failure);
    }
}
