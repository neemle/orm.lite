// §803 provenance: item ORML-138, pass FULL, round 3. Reviewer-authored proposal, placed unchanged (QA screening run, not committed).
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class IntegrityRetryBoundaryR3AttackTests
{
    [Fact(DisplayName =
        "R3-F1 803 FULL round 3: cancellation in retry classification retains the native failure")]
    public async Task Cancellation_in_retry_classification_keeps_the_cause()
    {
        await using var db =
            await SqliteMaintenanceDatabase.OpenWalAsync();
        using var cancellation = new CancellationTokenSource();

        var port = new RetryPort(_ => cancellation.Cancel());
        await using var source = new ProbeSource(
            db.Context.DataSource,
            () => throw new SqliteException("R3 retry contention", 5));
        await using var context = new OrmContext(source, port);
        db.Sink.Clear();

        var timeout = TimeSpan.FromSeconds(5);
        var cancelled =
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                context.Maintenance.CheckIntegrityAsync(
                    new IntegrityCheckRequest(IntegrityCheckMode.Full)
                    {
                        Timeout = timeout
                    },
                    cancellation.Token)
                .WaitAsync(timeout + SqliteMaintenanceDatabase.Margin));

        Assert.NotNull(port.Classified);
        Assert.Equal(cancellation.Token, cancelled.CancellationToken);
        await AssertReleasedAsync(db, source);

        // Head loses this exception in the retry loop and instead propagates
        // the cancellation exception raised by the next attempt.
        Assert.Same(port.Classified, cancelled.InnerException);
    }

    [Fact(DisplayName =
        "R3-F2 803 FULL round 3: classification spending the budget starts no further attempt")]
    public async Task Expiry_in_retry_classification_prevents_another_attempt()
    {
        await using var db =
            await SqliteMaintenanceDatabase.OpenWalAsync();

        var timeout = TimeSpan.FromSeconds(3);
        var contended = true;
        var successfulExecutions = 0;

        var port = new RetryPort(_ =>
        {
            // Model the classifier being suspended across the deadline.
            // Waiting a full request timeout guarantees the existing budget
            // has expired, independent of how long opening took.
            Thread.Sleep(timeout + TimeSpan.FromMilliseconds(100));
            contended = false;
        });

        await using var source = new ProbeSource(
            db.Context.DataSource,
            () =>
            {
                if (contended)
                    throw new SqliteException("R3 retry contention", 5);

                successfulExecutions++;
                return "ok";
            });
        await using var context = new OrmContext(source, port);
        db.Sink.Clear();

        var clock = Stopwatch.StartNew();
        var result = await context.Maintenance.CheckIntegrityAsync(
            new IntegrityCheckRequest(IntegrityCheckMode.Full)
            {
                Timeout = timeout
            });
        clock.Stop();

        Assert.NotNull(port.Classified);
        Assert.True(
            clock.Elapsed < timeout + SqliteMaintenanceDatabase.Margin,
            "Elapsed: " + clock.Elapsed);
        await AssertReleasedAsync(db, source);

        // Head performs another attempt, receives "ok", and returns Completed.
        Assert.Equal(MaintenanceStatus.Busy, result.Status);
        Assert.Equal(0, successfulExecutions);
    }

    private static async Task AssertReleasedAsync(
        SqliteMaintenanceDatabase db, ProbeSource source)
    {
        Assert.NotNull(source.Opened);
        Assert.Equal(ConnectionState.Closed, source.Opened!.State);
        Assert.Equal(
            MaintenanceOperation.IntegrityCheck,
            Assert.Single(db.Sink.Maintenance).Operation);

        var next = await db.Maintenance.CheckIntegrityAsync(
            new IntegrityCheckRequest(IntegrityCheckMode.Full)
            {
                Timeout = TimeSpan.FromSeconds(5)
            }).WaitAsync(
                TimeSpan.FromSeconds(5) + SqliteMaintenanceDatabase.Margin);

        Assert.Equal(MaintenanceStatus.Completed, next.Status);
    }

    private sealed class RetryPort(Action<DbException> onFirstContention)
        : ISqlProvider, IMaintenanceProvider
    {
        private static readonly SqliteProvider Inner =
            SqliteProvider.Instance;

        internal DbException? Classified { get; private set; }

        public string Id => "integrity-retry-boundary";
        public CapabilitySet Capabilities => Inner.Capabilities;
        public ISerializer CreateSerializer() => Inner.CreateSerializer();
        public TimeSpan MaintenanceTimeout => Inner.MaintenanceTimeout;
        public TimeSpan? ContentionRetrySlice => TimeSpan.FromSeconds(1);

        public string? WriterKeyOf(DbDataSource source) =>
            Inner.WriterKeyOf(source);

        public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(
            DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
            Inner.TryEnterWriteAsync(source, timeout, ct);

        public bool IsContention(DbException failure)
        {
            var contention = Inner.IsContention(failure);
            if (contention && Classified is null)
            {
                Classified = failure;
                onFirstContention(failure);
            }
            return contention;
        }

        public string RenderIntegrityCheck(
            IntegrityCheckMode mode, EntityDescriptor? table,
            int maxFindings) =>
            "SELECT r3_integrity_probe()";

        public IntegrityFinding? ReadIntegrityFinding(
            IntegrityCheckMode mode, DbDataReader row) =>
            Inner.ReadIntegrityFinding(mode, row);
    }

    private sealed class ProbeSource(
        DbDataSource inner, Func<string> execute) : DbDataSource
    {
        internal DbConnection? Opened { get; private set; }

        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            throw new NotSupportedException();

        protected override async ValueTask<DbConnection>
            OpenDbConnectionAsync(
                CancellationToken cancellationToken = default)
        {
            var connection =
                await inner.OpenConnectionAsync(cancellationToken);
            try
            {
                var sqlite =
                    Assert.IsAssignableFrom<SqliteConnection>(connection);
                sqlite.CreateFunction("r3_integrity_probe", execute);
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
