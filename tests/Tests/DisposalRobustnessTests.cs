using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests;

public sealed class DisposalRobustnessTests
{
    private sealed class TrackedConnection(string connectionString) : SqliteConnection(connectionString)
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class TrackedSource : DbDataSource
    {
        public TrackedConnection? Connection { get; private set; }

        public override string ConnectionString => "Data Source=:memory:";

        protected override DbConnection CreateDbConnection() =>
            Connection = new TrackedConnection(ConnectionString);
    }

    private sealed class FailingTransaction(DbConnection connection) : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.Serializable;

        protected override DbConnection DbConnection => connection;

        public override void Commit()
        {
        }

        public override void Rollback()
        {
        }

        public override ValueTask DisposeAsync() => throw new InvalidOperationException("the rollback failed");
    }

    private sealed class FailingProvider : ISqlProvider
    {
        public string Id => "failing";

        public CapabilitySet Capabilities { get; } = CapabilitySet.Everything();

        public ISerializer CreateSerializer() => throw new NotSupportedException();

        public ValueTask<ProviderTransaction> BeginTransactionAsync(
            DbConnection connection, DbDataSource source, CancellationToken ct = default) =>
            ValueTask.FromResult(new ProviderTransaction(new FailingTransaction(connection), null));
    }

    [Fact(DisplayName = "a unit of work whose transaction fails to dispose still disposes its connection, "
        + "reports the failure once, and is quiet the second time")]
    public async Task A_failing_transaction_does_not_leak_the_connection()
    {
        var source = new TrackedSource();
        var unit = await UnitOfWork.BeginAsync(new OrmContext(source, new FailingProvider()));
        Assert.False(source.Connection!.WasDisposed);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () => await unit.DisposeAsync());

        Assert.Equal("the rollback failed", failure.Message);
        Assert.True(source.Connection.WasDisposed);
        await unit.DisposeAsync();
    }

    [Fact(DisplayName = "an advisory lease disposed twice unlocks once, disposes its connection "
        + "and does not throw")]
    public async Task A_lease_disposes_once()
    {
        var connection = new TrackedConnection("Data Source=:memory:");
        var unlocks = 0;
        var lease = new AdvisorySessionLease(connection, _ => { unlocks++; return Task.CompletedTask; });

        await lease.DisposeAsync();
        await lease.DisposeAsync();

        Assert.Equal(1, unlocks);
        Assert.True(connection.WasDisposed);
    }

    [Theory(DisplayName = "an unlock that fails on a broken connection is swallowed "
        + "and the connection is still disposed")]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ObjectDisposedException))]
    public async Task An_unlock_failure_is_swallowed(Type failure)
    {
        var connection = new TrackedConnection("Data Source=:memory:");
        var lease = new AdvisorySessionLease(
            connection, _ => throw (Exception)Activator.CreateInstance(failure, "gone")!);

        await lease.DisposeAsync();

        Assert.True(connection.WasDisposed);
    }
}
