// §803 provenance: item ORML-136, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class ReindexExpiredBudgetR2AttackTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromMilliseconds(500);

    [Theory(DisplayName =
        "R2-F2 803 FULL round 2: an expired open or lookup cannot start REINDEX")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exhaustion_prevents_reindex(bool duringLookup)
    {
        await using var db = await ReindexFixture.OpenAsync();
        await using var source =
            new DelayedSource(db.Context.DataSource, duringLookup);

        ISqlProvider provider = duringLookup
            ? new LookupProvider()
            : db.Context.Provider;

        await using var context = new OrmContext(source, provider);
        db.Sink.Clear();

        var clock = Stopwatch.StartNew();
        var result = await context.Maintenance.ReindexAsync(
            new ReindexRequest(ReindexScope.Index, ReindexMode.Blocking)
            {
                Table = ReindexFixture.A,
                Index = ReindexFixture.Unique,
                Timeout = Budget,
            }).WaitAsync(Budget + SqliteMaintenanceDatabase.Margin);
        clock.Stop();

        var events = db.Sink.Maintenance.ToArray();

        // Verify that the expired call released the shared writer gate.
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
        var next = await db.Maintenance.ReindexAsync(
            ReindexFixture.Request(ReindexScope.Table, timeoutMs: 5000));
        Assert.Equal(MaintenanceStatus.Completed, next.Status);

        Assert.True(
            clock.Elapsed < Budget + SqliteMaintenanceDatabase.Margin,
            "Expired call took " + clock.Elapsed);
        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Empty(events);
    }

    private sealed class DelayedSource(
        DbDataSource inner, bool duringLookup) : DbDataSource
    {
        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            inner.CreateConnection();

        protected override async ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            var connection = await inner.OpenConnectionAsync(cancellationToken);
            try
            {
                if (duringLookup)
                {
                    ((SqliteConnection)connection).CreateFunction<string>(
                        "r2_schema", () =>
                        {
                            // This lookup starts within the budget but finishes
                            // after it. The subsequent REINDEX must not start.
                            Thread.Sleep(750);
                            return "main";
                        });
                }
                else
                {
                    // Model a successful open racing cancellation: ownership
                    // reaches the caller after its linked deadline has fired.
                    var expired = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    using var registration = cancellationToken.Register(
                        () => expired.TrySetResult());

                    await expired.Task.WaitAsync(
                        Budget + SqliteMaintenanceDatabase.Margin);
                }

                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }
    }

    private sealed class LookupProvider : ISqlProvider, IMaintenanceProvider
    {
        private static readonly SqliteProvider Inner = SqliteProvider.Instance;

        public string Id => "r2-lookup";
        public CapabilitySet Capabilities => Inner.Capabilities;
        public ISerializer CreateSerializer() => Inner.CreateSerializer();
        public TimeSpan MaintenanceTimeout => Inner.MaintenanceTimeout;
        public bool HasWriterGate => true;

        public string? WriterKeyOf(DbDataSource source) =>
            Inner.WriterKeyOf(source);

        public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(
            DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
            Inner.TryEnterWriteAsync(source, timeout, ct);

        public bool IsContention(DbException failure) =>
            Inner.IsContention(failure);

        public string? RenderIndexSchemaLookup(EntityDescriptor table) =>
            "SELECT r2_schema()";

        public string RenderReindex(
            ReindexScope scope, ReindexMode mode,
            EntityDescriptor? table, IndexDescriptor? index,
            string? indexSchema) =>
            Inner.RenderReindex(scope, mode, table, index, indexSchema);
    }
}
