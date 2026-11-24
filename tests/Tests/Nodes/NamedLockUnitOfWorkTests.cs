using System.Data.Common;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Neemle.OrmLite.Tests.Nodes;

public sealed class NamedLockUnitOfWorkTests
{
    private sealed class MemoryDataSource : DbDataSource
    {
        public override string ConnectionString => "Data Source=:memory:";

        protected override DbConnection CreateDbConnection() => new SqliteConnection(ConnectionString);
    }

    [Fact(DisplayName = "a unit of work over a provider without named locks refuses both lock kinds by capability, before any statement is sent")]
    public async Task A_provider_without_named_locks_refuses_both_kinds()
    {
        await using var work = await UnitOfWork.BeginAsync(TestContext.Of(new MemoryDataSource(), FixtureProvider.Instance));

        var exclusive = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => work.LockAsync("key"));
        var shared = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => work.LockSharedAsync("key"));

        Assert.Equal(Capability.NamedLock, exclusive.Capability);
        Assert.Equal(Capability.NamedLock, shared.Capability);
        Assert.Equal("fixture", exclusive.Provider);
    }

    [Fact(DisplayName = "a null key and a disposed unit of work are still refused as before, whatever the provider")]
    public async Task Argument_and_disposal_refusals_come_first()
    {
        var work = await UnitOfWork.BeginAsync(TestContext.Of(new MemoryDataSource(), FixtureProvider.Instance));

        await Assert.ThrowsAsync<ArgumentNullException>(() => work.LockAsync(null!));
        await work.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => work.LockSharedAsync("key"));
    }
}
