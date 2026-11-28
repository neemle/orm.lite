// §803 provenance: item ORML-138, pass FULL, round 2. Reviewer-authored proposal, placed unchanged.
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance;

public sealed class IntegrityCancellationBoundaryR2AttackTests
{
    [Fact(DisplayName =
        "R2-F2 803 FULL round 2: cancellation during final finding mapping wins over IssuesFound")]
    public async Task Cancellation_on_the_extra_finding_is_not_success()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        await IntegrityFixtures.ForeignKeyViolationsAsync(db, violations: 2);
        using var cancel = new CancellationTokenSource();
        var mapped = 0;

        var provider = new CallbackPort
        {
            OnFinding = () =>
            {
                if (++mapped == 2)
                    cancel.Cancel();
            }
        };
        await using var context =
            new OrmContext(db.Context.DataSource, provider);
        db.Sink.Clear();

        var failure = await Record.ExceptionAsync(async () =>
        {
            await context.Maintenance.CheckIntegrityAsync(
                IntegrityFixtures.Request(
                    IntegrityCheckMode.ForeignKeys, maxFindings: 1),
                cancel.Token);
        });

        Assert.Equal(2, mapped);
        Assert.Equal(
            MaintenanceOperation.IntegrityCheck,
            Assert.Single(db.Sink.Maintenance).Operation);

        Assert.Equal(
            MaintenanceStatus.Completed,
            (await db.Maintenance.CheckIntegrityAsync(
                new IntegrityCheckRequest(IntegrityCheckMode.Full))).Status);

        var cancelled =
            Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(cancel.Token, cancelled.CancellationToken);
    }

    [Fact(DisplayName =
        "R2-F2 803 FULL round 2: cancellation during contention classification preserves the exception")]
    public async Task Classification_race_keeps_the_original_failure()
    {
        await using var db = await SqliteMaintenanceDatabase.OpenWalAsync();
        using var cancel = new CancellationTokenSource();
        var original = new SqliteException("busy during open", 5);
        DbException? classified = null;

        var provider = new CallbackPort
        {
            OnContention = failure =>
            {
                classified = failure;
                cancel.Cancel();
            }
        };
        await using var source =
            new FailingOpenSource(db.Context.DataSource, original);
        await using var context = new OrmContext(source, provider);
        db.Sink.Clear();

        var cancelled = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => context.Maintenance.CheckIntegrityAsync(
                new IntegrityCheckRequest(IntegrityCheckMode.Full),
                cancel.Token));

        Assert.Same(original, classified);
        Assert.Equal(cancel.Token, cancelled.CancellationToken);
        Assert.Empty(db.Sink.Maintenance);
        Assert.Same(original, cancelled.InnerException);
    }

    private sealed class CallbackPort : ISqlProvider, IMaintenanceProvider
    {
        private static readonly SqliteProvider Inner = SqliteProvider.Instance;

        internal Action? OnFinding { get; init; }
        internal Action<DbException>? OnContention { get; init; }

        public string Id => "integrity-callback";
        public CapabilitySet Capabilities => Inner.Capabilities;
        public ISerializer CreateSerializer() => Inner.CreateSerializer();
        public TimeSpan MaintenanceTimeout => Inner.MaintenanceTimeout;

        public string? WriterKeyOf(DbDataSource source) =>
            Inner.WriterKeyOf(source);

        public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(
            DbDataSource source, TimeSpan timeout, CancellationToken ct) =>
            Inner.TryEnterWriteAsync(source, timeout, ct);

        public bool IsContention(DbException failure)
        {
            var contention = Inner.IsContention(failure);
            if (contention)
                OnContention?.Invoke(failure);
            return contention;
        }

        public string RenderIntegrityCheck(
            IntegrityCheckMode mode, EntityDescriptor? table, int maxFindings) =>
            Inner.RenderIntegrityCheck(mode, table, maxFindings);

        public IntegrityFinding? ReadIntegrityFinding(
            IntegrityCheckMode mode, DbDataReader row)
        {
            var finding = Inner.ReadIntegrityFinding(mode, row);
            if (finding is not null)
                OnFinding?.Invoke();
            return finding;
        }

        public int? IntegrityEngineCap(
            IntegrityCheckMode mode, EntityDescriptor? table) =>
            Inner.IntegrityEngineCap(mode, table);
    }

    private sealed class FailingOpenSource(
        DbDataSource inner, DbException failure) : DbDataSource
    {
        public override string ConnectionString => inner.ConnectionString;

        protected override DbConnection CreateDbConnection() =>
            throw new NotSupportedException();

        protected override ValueTask<DbConnection> OpenDbConnectionAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<DbConnection>(failure);
    }
}
