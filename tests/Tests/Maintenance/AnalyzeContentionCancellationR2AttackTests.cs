// §803 provenance: item ORML-135, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class AnalyzeContentionCancellationR2AttackTests
{
    [Fact(DisplayName =
        "R2-F1 — 803 FULL round 2 — cancellation during contention classification preserves the native exception")]
    public async Task Cancellation_preserves_the_open_contention_exception()
    {
        await using var db = await AnalyzeFixture.OpenAsync();
        await db.ExecAsync(WalFixture.Items);

        using var cancellation = new CancellationTokenSource();
        var native = new SqliteException("injected open contention", 5);
        await using var source =
            new FailingOpenSource(db.Context.DataSource, native);
        var provider = new CancellingClassifier(cancellation);
        await using var context = new OrmContext(source, provider);

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Maintenance.AnalyzeAsync(
                AnalyzeFixture.Table(AnalyzeFixture.A, 5000),
                cancellation.Token));

        Assert.True(provider.ClassifiedContention);
        Assert.Equal(cancellation.Token, failure.CancellationToken);

        // Recovery also verifies that the failed open released the real gate.
        var next = await db.Maintenance.AnalyzeAsync(
            AnalyzeFixture.Table(AnalyzeFixture.A, 5000));
        Assert.Equal(MaintenanceStatus.Completed, next.Status);
        await db.WriteAsync().WaitAsync(
            TimeSpan.FromSeconds(5) + SqliteMaintenanceDatabase.Margin);

        // Head throws cancellation with InnerException == null.
        Assert.Same(native, failure.InnerException);
    }

    private sealed class FailingOpenSource(
        DbDataSource inner, DbException failure) : DbDataSource
    {
        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            throw new NotSupportedException();

        protected override ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromException<DbConnection>(failure);
        }
    }

    private sealed class CancellingClassifier(
        CancellationTokenSource cancellation)
        : ISqlProvider, IMaintenanceProvider
    {
        private static SqliteProvider Inner => SqliteProvider.Instance;

        internal bool ClassifiedContention { get; private set; }

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

        public string RenderAnalyze(
            AnalyzeScope scope, EntityDescriptor? table) =>
            Inner.RenderAnalyze(scope, table);

        public bool IsContention(DbException failure)
        {
            var contention = Inner.IsContention(failure);
            if (contention)
            {
                ClassifiedContention = true;
                // Deterministically exercise cancellation arriving while
                // the pipeline classifies a native contention exception.
                cancellation.Cancel();
            }

            return contention;
        }
    }
}
