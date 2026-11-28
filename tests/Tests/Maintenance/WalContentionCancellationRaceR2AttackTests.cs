// §803 provenance: item ORML-137, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
// HELD (§803.8): admitted FAILS, routed by NINE-FIVE-ROUTING 9199497d on ORML-137 to fix list item 3, the Busy cause
// (TL fix list 6b33e96e on ORML-137). The Skip below is the HELD marker; the commit that turns it green removes it.
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class WalContentionCancellationRaceR2AttackTests
{
    [Theory(Skip = "HELD: routing 9199497d on ORML-137 and ORML-137 (fix list 6b33e96e item 3)", DisplayName =
        "R2-F2 803 FULL round 2: cancellation during classification retains failure")]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Cancellation_preserves_the_original_open_failure(int code)
    {
        await using var db = await WalFixture.WithItemsAsync(1);
        using var cancellation = new CancellationTokenSource();
        var original = new SqliteException("opening met contention", code);

        await using var source = new FailingSource(
            db.Context.DataSource.ConnectionString, original);
        await using var context = new OrmContext(
            source, new CancellingClassifier(cancellation));

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Maintenance.CheckpointWalAsync(
                WalFixture.Request(WalCheckpointMode.Truncate, 5000),
                cancellation.Token));

        // Verify gate recovery before checking the expected regression.
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);

        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.Same(original, failure.InnerException);
        Assert.Empty(db.Sink.Maintenance);
    }

    private sealed class FailingSource(
        string connectionString, DbException failure) : DbDataSource
    {
        public override string ConnectionString => connectionString;

        protected override DbConnection CreateDbConnection() =>
            throw new NotSupportedException();

        protected override ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<DbConnection>(failure);
    }

    private sealed class CancellingClassifier(
        CancellationTokenSource cancellation) : ISqlProvider, IMaintenanceProvider
    {
        private static readonly SqliteProvider Inner = SqliteProvider.Instance;

        public string Id => "sqlite-cancellation-race";
        public CapabilitySet Capabilities => Inner.Capabilities;
        public ISerializer CreateSerializer() => Inner.CreateSerializer();
        public TimeSpan MaintenanceTimeout => Inner.MaintenanceTimeout;

        public string? WriterKeyOf(DbDataSource source) =>
            Inner.WriterKeyOf(source);

        public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(
            DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
            Inner.TryEnterWriteAsync(source, timeout, ct);

        public string RenderWalCheckpoint(WalCheckpointMode mode) =>
            Inner.RenderWalCheckpoint(mode);

        public bool IsContention(DbException failure)
        {
            var contention = Inner.IsContention(failure);

            // Deterministically place cancellation between the caller's
            // initial token check and its conversion of this failure to Busy.
            cancellation.Cancel();
            return contention;
        }
    }
}
