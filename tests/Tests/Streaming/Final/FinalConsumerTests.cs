using Xunit;

namespace Neemle.OrmLite.Tests.Streaming.Final;

public sealed class FinalConsumerTests
{
    [Fact(DisplayName = "[UC-010][AC01] ordinary async-enumerable consumption stays cold and pulls one row at a time")]
    public async Task Interface_foreach_yields_detached_prefix_without_read_ahead()
    {
        var p = new StreamProbe();
        IAsyncEnumerable<StreamRow> sequence = p.Query().StreamAsync();
        Assert.Equal((0, 0, 0), (p.Connections, p.Executions, p.Reads));
        var ids = new List<int>();
        await foreach (var row in sequence)
        {
            ids.Add(row.Id);
            Assert.Equal(ids.Count, p.Reads);
            await Task.Yield();
            Assert.Equal(ids.Count, p.Reads);
        }
        Assert.Equal(new[] { 1, 2, 3 }, ids);
        Assert.Equal((1, 4, 3), (p.Executions, p.Reads, p.Maps));
        Assert.Equal((1, 1, 1), (p.ReaderDisposals, p.CommandDisposals, p.ConnectionDisposals));
    }

    [Fact(DisplayName = "[UC-010][AC04] default await-foreach preserves its body error when every cleanup stage fails")]
    public async Task Foreach_body_exception_is_not_replaced_by_hidden_cleanup()
    {
        var body = new InvalidOperationException("consumer body");
        var p = new StreamProbe { ReaderError = new IOException("reader"),
            CommandError = new IOException("command"), ConnectionError = new IOException("connection") };
        IAsyncEnumerable<StreamRow> sequence = p.Query().StreamAsync();
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var row in sequence)
            {
                Assert.Equal(1, row.Id);
                throw body;
            }
        });
        Assert.Same(body, observed);
        Assert.Equal((1, 1, 1), (p.ReaderDisposals, p.CommandDisposals, p.ConnectionDisposals));
        Assert.Equal((1, 1), (p.Executions, p.Reads));
        // Default foreach hides Completion; attempted disposal is not a claim of successful cleanup.
    }

    [Fact(DisplayName =
        "[UC-010][SEC02] interface cancellation retains sensitive cleanup evidence with safe formatting")]
    public async Task Interface_token_and_canceled_evidence_keep_their_contract()
    {
        var cleanup = new IOException("password=private-cancel-cleanup");
        var p = new StreamProbe { ReaderError = cleanup };
        using var cancel = new CancellationTokenSource();
        IAsyncEnumerable<StreamRow> sequence = p.Query().StreamAsync();
        await using var e = sequence.GetAsyncEnumerator(cancel.Token);
        Assert.True(await e.MoveNextAsync());
        cancel.Cancel();
        var fault = await Assert.ThrowsAsync<QueryStreamCanceledException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(cancel.Token, fault.CancellationToken);
        Assert.Equal(1, fault.RowsYielded);
        Assert.Same(cleanup, Assert.Single(fault.CleanupErrors));
        Assert.DoesNotContain("private", fault.ToString());
        await e.DisposeAsync();
        var typed = Assert.IsType<QueryStreamEnumerator<StreamRow>>(e);
        var end = await typed.Completion;
        Assert.Equal(QueryStreamStopReason.Canceled, end.Reason);
        Assert.False(end.CleanupSucceeded);
        Assert.Same(cleanup, Assert.Single(end.CleanupErrors));
        Assert.Same(fault, await Assert.ThrowsAsync<QueryStreamCanceledException>(() => e.MoveNextAsync().AsTask()));
        Assert.Equal(1, p.Executions);
    }
}
