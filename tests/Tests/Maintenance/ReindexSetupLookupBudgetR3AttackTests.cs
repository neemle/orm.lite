// §803 provenance: item ORML-136, pass FULL, round 3. Reviewer-authored proposal, placed unchanged (QA screening run, not committed).
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class ReindexSetupLookupBudgetR3AttackTests
{
    [Theory(DisplayName =
        "R3-F2 803 FULL round 3: exhausted setup starts no schema lookup and still tears down")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exhausted_setup_prevents_lookup(bool lookupWouldFail)
    {
        await using var db = await ReindexFixture.OpenAsync();
        var before = await ReindexFixture.DumpAsync(db);
        var budget = TimeSpan.FromMilliseconds(500);

        await using var source = new ProbeSource(
            db.Context.DataSource,
            budget + TimeSpan.FromMilliseconds(250),
            lookupWouldFail);
        await using var context =
            new OrmContext(source, new LookupProvider());
        db.Sink.Clear();

        ReindexResult? result = null;
        var clock = Stopwatch.StartNew();
        var failure = await Record.ExceptionAsync(async () =>
        {
            result = await context.Maintenance.ReindexAsync(
                ReindexFixture.Request(
                    ReindexScope.Index, timeoutMs: 500))
                .WaitAsync(budget + SqliteMaintenanceDatabase.Margin);
        });
        clock.Stop();
        var events = db.Sink.Maintenance.ToArray();

        // The operation must release its connection and writer gate,
        // including when setup exhausts the budget.
        Assert.NotNull(source.Opened);
        Assert.Equal(ConnectionState.Closed, source.Opened!.State);
        await db.WriteAsync().WaitAsync(SqliteMaintenanceDatabase.Margin);
        var next = await db.Maintenance.ReindexAsync(
            ReindexFixture.Request(ReindexScope.Table, timeoutMs: 5000));
        Assert.Equal(MaintenanceStatus.Completed, next.Status);
        Assert.Equal(before, await ReindexFixture.DumpAsync(db));

        Assert.True(
            clock.Elapsed < budget + SqliteMaintenanceDatabase.Margin,
            "The exhausted operation took " + clock.Elapsed);
        Assert.Equal(1, source.SetupCalls);
        Assert.Equal(1, source.TeardownCalls);
        Assert.Empty(events);
        Assert.Null(failure);
        Assert.NotNull(result);
        Assert.Equal(MaintenanceStatus.Busy, result!.Status);
        Assert.Equal(0, source.LookupCalls);
    }

    private sealed class LookupProvider : ISqlProvider, IMaintenanceProvider
    {
        private static readonly SqliteProvider Inner = SqliteProvider.Instance;

        public string Id => "r3-setup-lookup";
        public CapabilitySet Capabilities => Inner.Capabilities;
        public ISerializer CreateSerializer() => Inner.CreateSerializer();
        public TimeSpan MaintenanceTimeout => Inner.MaintenanceTimeout;
        public bool HasWriterGate => Inner.HasWriterGate;

        public string? WriterKeyOf(DbDataSource source) =>
            Inner.WriterKeyOf(source);

        public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(
            DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
            Inner.TryEnterWriteAsync(source, timeout, ct);

        public bool IsContention(DbException failure) =>
            Inner.IsContention(failure);

        public MaintenanceSessionGuard? RenderLockGuard(TimeSpan remaining) =>
            new("SELECT r3_setup()", "SELECT r3_teardown()");

        public string? RenderIndexSchemaLookup(EntityDescriptor table) =>
            "SELECT r3_schema()";

        public string RenderReindex(
            ReindexScope scope, ReindexMode mode,
            EntityDescriptor? table, IndexDescriptor? index,
            string? indexSchema) =>
            Inner.RenderReindex(scope, mode, table, index, indexSchema);
    }

    private sealed class ProbeSource(
        DbDataSource inner,
        TimeSpan setupDelay,
        bool lookupWouldFail) : DbDataSource
    {
        internal DbConnection? Opened { get; private set; }
        internal int SetupCalls { get; private set; }
        internal int LookupCalls { get; private set; }
        internal int TeardownCalls { get; private set; }

        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            throw new NotSupportedException();

        protected override async ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default)
        {
            var connection = await inner.OpenConnectionAsync(cancellationToken);
            try
            {
                var sqlite = Assert.IsAssignableFrom<SqliteConnection>(connection);
                sqlite.CreateFunction<int>("r3_setup", () =>
                {
                    SetupCalls++;
                    Thread.Sleep(setupDelay);
                    return 0;
                });
                sqlite.CreateFunction<string>("r3_schema", () =>
                {
                    LookupCalls++;
                    if (lookupWouldFail)
                        throw new SqliteException(
                            "The expired call executed its schema lookup.", 1);
                    return "main";
                });
                sqlite.CreateFunction<int>("r3_teardown", () =>
                {
                    TeardownCalls++;
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
    }
}
