using System.Data;
using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming.Final;

public sealed class FinalLifetimeTests
{
    [Fact(DisplayName = "[UC-010][AC07] stream-only lease does not intercept legacy buffered commands")]
    public async Task Legacy_command_reaches_provider_while_new_stream_is_live()
    {
        var p = new StreamProbe();
        await using var connection = new StreamProbeConnection(p);
        await connection.OpenAsync();
        var query = p.Query().On(connection);
        var e = query.StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        Assert.Equal(3, (await query.ToListAsync()).Count);
        Assert.Equal((2, 1), (p.Executions, p.ReaderDisposals));
        Assert.True(await e.MoveNextAsync());
        Assert.Equal(2, e.Current.Id);
        await e.DisposeAsync();
        Assert.Equal(0, p.ConnectionDisposals);
        Assert.True((await e.Completion).CleanupSucceeded);
    }

    [Fact(DisplayName = "[UC-010][AC07] hand-built context disposal adds no global borrowed-stream drain")]
    public async Task Context_without_owned_handles_does_not_retire_caller_connection()
    {
        var p = new StreamProbe();
        _ = p.Query();
        await using var connection = new StreamProbeConnection(p);
        await connection.OpenAsync();
        var context = new OrmContext(new StreamProbeSource(p), PostgresProvider.Instance);
        var e = FinalQueries.Query(context).On(connection).StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        await context.DisposeAsync();
        Assert.Equal((0, 0, 0), (p.ReaderDisposals, p.CommandDisposals, p.ConnectionDisposals));
        Assert.True(await e.MoveNextAsync());
        await e.DisposeAsync();
        Assert.Equal(ConnectionState.Open, connection.State);
        Assert.True((await e.Completion).CleanupSucceeded);
    }

    [Theory(DisplayName = "[UC-010][AC06] UoW lifetime changes are not a global stream drain or native repair")]
    [InlineData(false)] [InlineData(true)]
    public async Task Uow_commit_or_disposal_is_observed_on_next_advance(bool dispose)
    {
        var p = new StreamProbe();
        var context = new OrmContext(new StreamProbeSource(p), new StreamProbeProvider(p));
        _ = p.Query();
        await using var unit = await UnitOfWork.BeginAsync(context);
        var e = FinalQueries.Query(context).On(unit).StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        if (dispose) await unit.DisposeAsync();
        else await unit.CommitAsync();
        Assert.Equal((0, 0), (p.ReaderDisposals, p.CommandDisposals));
        var fault = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.ResourceUnavailable, fault.Error);
        await e.DisposeAsync();
        Assert.Equal((1, 1, 1), (p.Executions, p.ReaderDisposals, p.CommandDisposals));
        Assert.Equal(dispose ? 1 : 0, p.ConnectionDisposals);
        Assert.Equal(1, (await e.Completion).RowsYielded);
        Assert.Equal(QueryStreamStopReason.Faulted, (await e.Completion).Reason);
    }

    [Fact(DisplayName = "[UC-010][SEC01] recorded UoW provider mismatch never executes or replaces caller resources")]
    public async Task Different_provider_binding_refuses_before_command()
    {
        var p = new StreamProbe();
        var context = new OrmContext(new StreamProbeSource(p), new StreamProbeProvider(p));
        await using var unit = await UnitOfWork.BeginAsync(context);
        var query = FinalQueries.Query(p, new FinalCapabilityProvider(CapabilitySet.Everything())).On(unit);
        var e = query.StreamAsync().GetAsyncEnumerator();
        var fault = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.ResourceMismatch, fault.Error);
        await e.DisposeAsync();
        Assert.Equal((0, 0, 0), (p.Commands, p.Executions, p.ConnectionDisposals));
        Assert.False(unit.Committed);
        Assert.Equal(ConnectionState.Open, unit.Connection.State);
        Assert.True((await e.Completion).CleanupSucceeded);
    }

    [Fact(DisplayName = "[UC-010][AC03] explicit native rebind clears previous UoW provenance")]
    public async Task Native_rebind_does_not_keep_a_committed_old_scope()
    {
        var p = new StreamProbe();
        var context = new OrmContext(new StreamProbeSource(p), new StreamProbeProvider(p));
        await using var unit = await UnitOfWork.BeginAsync(context);
        _ = p.Query();
        var query = FinalQueries.Query(context).On(unit);
        await using var native = new StreamProbeConnection(p);
        await native.OpenAsync();
        await using var transaction = await native.BeginTransactionAsync();
        query.On(native, transaction);
        await unit.CommitAsync();
        var e = query.StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        Assert.Same(transaction, p.Transaction);
        await e.DisposeAsync();
        Assert.True((await e.Completion).CleanupSucceeded);
        Assert.Equal(ConnectionState.Open, native.State);
        Assert.Equal((1, 0), (p.Executions, p.ConnectionDisposals));
    }
}
