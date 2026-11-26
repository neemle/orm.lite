using System.Data;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming.Sdet;

public sealed class SdetBorrowedBoundaryTests
{
    [Fact(DisplayName = "[UC-010] borrowed mismatched transaction refuses before command acquisition")]
    public async Task Different_transaction_connection_never_executes_or_disposes_caller_handles()
    {
        var p = new StreamProbe();
        await using var first = new StreamProbeConnection(p);
        await using var second = new StreamProbeConnection(p);
        await first.OpenAsync(); await second.OpenAsync();
        await using var tx = await second.BeginTransactionAsync();
        var e = p.Query().On(first, tx).StreamAsync().GetAsyncEnumerator();
        var fault = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.ResourceMismatch, fault.Error);
        Assert.Equal((0, 0, 0), (p.Commands, p.Executions, p.ConnectionDisposals));
        Assert.Equal(ConnectionState.Open, first.State);
        Assert.Equal(ConnectionState.Open, second.State);
        await e.DisposeAsync();
        Assert.True((await e.Completion).CleanupSucceeded);
    }

    [Fact(DisplayName = "[UC-010] command cleanup uncertainty retires borrowed streaming independently of reader success")]
    public async Task Command_failure_poison_is_local_to_exact_borrowed_identity()
    {
        var p = new StreamProbe { CommandError = new IOException("command") };
        await using var connection = new StreamProbeConnection(p);
        await connection.OpenAsync();
        var stream = p.Query().On(connection).StreamAsync();
        var first = stream.GetAsyncEnumerator();
        Assert.True(await first.MoveNextAsync());
        await first.DisposeAsync();
        Assert.False((await first.Completion).CleanupSucceeded);
        var second = stream.GetAsyncEnumerator();
        var fault = await Assert.ThrowsAsync<QueryStreamException>(() => second.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.ResourceUnavailable, fault.Error);
        Assert.Equal(1, p.Commands);
        Assert.Equal(0, p.ConnectionDisposals);
        p.CommandError = null;
        await using var independent = new StreamProbeConnection(p);
        await independent.OpenAsync();
        var third = p.Query().On(independent).StreamAsync().GetAsyncEnumerator();
        Assert.True(await third.MoveNextAsync());
        await third.DisposeAsync(); await second.DisposeAsync();
        Assert.True((await third.Completion).CleanupSucceeded);
    }

    [Fact(DisplayName = "[UC-010] row-lock capture requires an actual borrowed transaction")]
    public async Task Explicit_lock_without_transaction_is_cold_failure()
    {
        var p = new StreamProbe();
        var fault = Assert.Throws<QueryStreamException>(() => p.Query().ForUpdate().StreamAsync());
        Assert.Equal(QueryStreamError.ResourceMismatch, fault.Error);
        await using var c = new StreamProbeConnection(p);
        await c.OpenAsync();
        Assert.Throws<QueryStreamException>(() => p.Query().On(c).ForUpdate().StreamAsync());
        await using var tx = await c.BeginTransactionAsync();
        var e = p.Query().On(c, tx).ForUpdate().StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        Assert.Same(tx, p.Transaction);
        Assert.Contains("FOR UPDATE", p.Sql);
        await e.DisposeAsync();
        Assert.Equal(0, p.ConnectionDisposals);
    }
}
