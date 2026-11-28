// §803 provenance: item ORML-136, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
// HELD (§803.8): admitted FAILS, routed by NINE-FIVE-ROUTING f0f156f4 on ORML-136 to fix list item 3, the Busy cause
// (TL fix list 6b33e96e on ORML-137). The Skip below is the HELD marker; the commit that turns it green removes it.
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class ReindexCancellationRaceR2AttackTests
{
    [Fact(Skip = "HELD: routing f0f156f4 on ORML-136 and ORML-137 (fix list 6b33e96e item 3)", DisplayName =
        "R2-F3 803 FULL round 2: cancellation during contention mapping preserves the native failure")]
    public async Task Cancellation_keeps_the_contention_exception()
    {
        await using var db = await ReindexFixture.OpenAsync();
        using var cancel = new CancellationTokenSource();
        var provider = new CancellingClassifier(cancel);
        await using var context =
            new OrmContext(db.Context.DataSource, provider);

        await using var blocker = await db.RawAsync();
        await using var transaction = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(
            blocker, "INSERT INTO b (k) VALUES ('blocker')", transaction);

        Exception? failure;
        try
        {
            failure = await Record.ExceptionAsync(async () =>
            {
                await context.Maintenance.ReindexAsync(
                    ReindexFixture.Request(
                        ReindexScope.Table, timeoutMs: 200),
                    cancel.Token);
            });
        }
        finally
        {
            await transaction.RollbackAsync();
        }

        // Cleanup must succeed even though cancellation won the prior call.
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
        var next = await db.Maintenance.ReindexAsync(
            ReindexFixture.Request(ReindexScope.Table, timeoutMs: 5000));
        Assert.Equal(MaintenanceStatus.Completed, next.Status);

        var cancelled =
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(cancel.Token, cancelled.CancellationToken);
        Assert.IsType<SqliteException>(provider.Seen);
        Assert.Same(provider.Seen, cancelled.InnerException);
    }

    private sealed class CancellingClassifier(
        CancellationTokenSource cancel) : ISqlProvider, IMaintenanceProvider
    {
        private static readonly SqliteProvider Inner = SqliteProvider.Instance;

        internal DbException? Seen { get; private set; }

        public string Id => "r2-cancelling-classifier";
        public CapabilitySet Capabilities => Inner.Capabilities;
        public ISerializer CreateSerializer() => Inner.CreateSerializer();
        public TimeSpan MaintenanceTimeout => Inner.MaintenanceTimeout;
        public bool HasWriterGate => true;

        public string? WriterKeyOf(DbDataSource source) =>
            Inner.WriterKeyOf(source);

        public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(
            DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
            Inner.TryEnterWriteAsync(source, timeout, ct);

        public bool IsContention(DbException failure)
        {
            if (!Inner.IsContention(failure)) return false;
            Seen = failure;
            cancel.Cancel();
            return true;
        }

        // The default null retry slice is valid for a maintenance provider.
        // It places the classification in the final contention mapping.
        public string RenderReindex(
            ReindexScope scope, ReindexMode mode,
            EntityDescriptor? table, IndexDescriptor? index,
            string? indexSchema) =>
            Inner.RenderReindex(scope, mode, table, index, indexSchema);
    }
}
