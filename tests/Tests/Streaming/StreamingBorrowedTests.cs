using System.Data;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming;

public sealed class StreamingBorrowedTests
{
    [Fact(DisplayName = "[UC-010] borrowed streams use exact caller handles and reject a second live stream")]
    public async Task Borrowed_guard_does_not_take_connection_or_transaction_ownership()
    {
        var probe = new StreamProbe();
        await using var connection = new StreamProbeConnection(probe);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var stream = probe.Query().On(connection, transaction).StreamAsync();
        await using var first = stream.GetAsyncEnumerator();
        await using var second = stream.GetAsyncEnumerator();
        Assert.True(await first.MoveNextAsync());
        var error = await Assert.ThrowsAsync<QueryStreamException>(() => second.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.ConcurrentStream, error.Error);
        Assert.Equal(1, probe.Commands);
        Assert.Same(transaction, probe.Transaction);
        await first.DisposeAsync();
        Assert.Equal(0, probe.ConnectionDisposals);
        Assert.Equal(ConnectionState.Open, connection.State);
        await using var third = stream.GetAsyncEnumerator();
        Assert.True(await third.MoveNextAsync());
        Assert.Equal(2, probe.Executions);
    }

    [Fact(DisplayName = "[UC-010] uncertain cleanup retires streams but does not guard native use")]
    public async Task Uncertain_cursor_retires_only_the_new_stream_path()
    {
        var probe = new StreamProbe { ReaderError = new IOException("private-cleanup") };
        await using var connection = new StreamProbeConnection(probe);
        await connection.OpenAsync();
        var stream = probe.Query().On(connection).StreamAsync();
        var e = stream.GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        await e.DisposeAsync();
        Assert.False((await e.Completion).CleanupSucceeded);
        await using var next = stream.GetAsyncEnumerator();
        var error = await Assert.ThrowsAsync<QueryStreamException>(() => next.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.ResourceUnavailable, error.Error);
        Assert.Equal(1, probe.Commands);
        Assert.Equal(ConnectionState.Open, connection.State);
        probe.ReaderError = null;
        await using var native = connection.CreateCommand();
        await using var reader = await native.ExecuteReaderAsync();
        Assert.Equal(2, probe.Executions);
    }
}
