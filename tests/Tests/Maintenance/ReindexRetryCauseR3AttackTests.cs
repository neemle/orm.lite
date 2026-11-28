// §803 provenance: item ORML-136, pass FULL, round 3. Reviewer-authored proposal, placed unchanged (QA screening run, not committed).
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class ReindexRetryCauseR3AttackTests
{
    [Fact(DisplayName =
        "R3-F1 803 FULL round 3: cancellation during retry classification retains the native cause")]
    public async Task Cancellation_during_retry_classification_keeps_the_cause()
    {
        await using var db = await ReindexFixture.OpenAsync();
        var before = await ReindexFixture.DumpAsync(db);
        using var cancel = new CancellationTokenSource();
        var provider = new CancellingRetryProvider(cancel);
        await using var context =
            new OrmContext(db.Context.DataSource, provider);

        await using var blocker = await db.RawAsync();
        await using var transaction = blocker.BeginTransaction(deferred: false);
        await SqliteMaintenanceDatabase.Exec(
            blocker, "INSERT INTO b (k) VALUES ('blocker')", transaction);
        db.Sink.Clear();

        Exception? failure;
        try
        {
            failure = await Record.ExceptionAsync(async () =>
            {
                await context.Maintenance.ReindexAsync(
                    ReindexFixture.Request(
                        ReindexScope.Table, timeoutMs: 10_000),
                    cancel.Token).WaitAsync(
                        TimeSpan.FromSeconds(10)
                        + SqliteMaintenanceDatabase.Margin);
            });
        }
        finally
        {
            await transaction.RollbackAsync();
        }

        var events = db.Sink.Maintenance.ToArray();

        // Verify cleanup before asserting the disputed exception property.
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
        var next = await db.Maintenance.ReindexAsync(
            ReindexFixture.Request(ReindexScope.Table, timeoutMs: 5000));
        Assert.Equal(MaintenanceStatus.Completed, next.Status);
        Assert.Equal(before, await ReindexFixture.DumpAsync(db));

        Assert.Equal(MaintenanceOperation.Reindex,
            Assert.Single(events).Operation);
        var cancelled =
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(cancel.Token, cancelled.CancellationToken);
        Assert.IsType<SqliteException>(provider.Seen);
        Assert.Same(provider.Seen, cancelled.InnerException);
    }

    private sealed class CancellingRetryProvider(
        CancellationTokenSource cancel) : ISqlProvider, IMaintenanceProvider
    {
        private static readonly SqliteProvider Inner = SqliteProvider.Instance;

        internal DbException? Seen { get; private set; }

        public string Id => "r3-cancelling-retry";
        public CapabilitySet Capabilities => Inner.Capabilities;
        public ISerializer CreateSerializer() => Inner.CreateSerializer();
        public TimeSpan MaintenanceTimeout => Inner.MaintenanceTimeout;
        public bool HasWriterGate => Inner.HasWriterGate;
        public TimeSpan? ContentionRetrySlice => Inner.ContentionRetrySlice;

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

        public string RenderReindex(
            ReindexScope scope, ReindexMode mode,
            EntityDescriptor? table, IndexDescriptor? index,
            string? indexSchema) =>
            Inner.RenderReindex(scope, mode, table, index, indexSchema);
    }
}
