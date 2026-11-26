using Xunit;

namespace Neemle.OrmLite.Tests.Streaming;

public sealed class StreamingCancellationTests
{
    [Fact(DisplayName = "[UC-010] terminal token wins deterministic cancellation before any database resource")]
    public async Task Precancel_is_cold_and_completion_is_not_success()
    {
        using var terminal = new CancellationTokenSource();
        using var enumeration = new CancellationTokenSource();
        var probe = new StreamProbe();
        var stream = probe.Query().StreamAsync(terminal.Token);
        await using var e = stream.GetAsyncEnumerator(enumeration.Token);
        terminal.Cancel(); enumeration.Cancel();
        var error = await Assert.ThrowsAsync<QueryStreamCanceledException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(terminal.Token, error.CancellationToken);
        Assert.Equal(0, probe.Connections);
        Assert.Equal(QueryStreamStopReason.Canceled, (await e.Completion).Reason);
        Assert.Same(error, await Assert.ThrowsAsync<QueryStreamCanceledException>(() => e.MoveNextAsync().AsTask()));
    }

    [Fact(DisplayName = "[UC-010] idle user cancellation waits until next advance and preserves its earlier prefix")]
    public async Task Cancellation_between_rows_is_observed_at_the_next_demand()
    {
        using var cancel = new CancellationTokenSource();
        var probe = new StreamProbe();
        await using var e = probe.Query().StreamAsync().GetAsyncEnumerator(cancel.Token);
        Assert.True(await e.MoveNextAsync());
        cancel.Cancel();
        Assert.Equal(0, probe.ReaderDisposals);
        var error = await Assert.ThrowsAsync<QueryStreamCanceledException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(cancel.Token, error.CancellationToken);
        Assert.Equal(1, error.RowsYielded);
        Assert.Equal((1, 1, 1), (probe.ReaderDisposals, probe.CommandDisposals, probe.ConnectionDisposals));
    }

    [Fact(DisplayName = "[UC-010] concurrent advance refuses while disposal awaits a driver that ignores cancellation")]
    public async Task Dispose_joins_actual_inflight_settlement_without_abandonment()
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new StreamProbe { IgnoreCancellation = true, BeforeRead = _ =>
        { reached.TrySetResult(); return release.Task; } };
        var e = probe.Query().StreamAsync().GetAsyncEnumerator();
        var advance = e.MoveNextAsync().AsTask();
        await reached.Task;
        var concurrent = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.ConcurrentAdvance, concurrent.Error);
        var dispose = e.DisposeAsync().AsTask();
        Assert.Same(dispose, e.DisposeAsync().AsTask());
        Assert.False(dispose.IsCompleted);
        Assert.Equal(0, probe.ReaderDisposals);
        release.SetResult();
        Assert.False(await advance);
        await dispose;
        Assert.Equal(QueryStreamStopReason.Disposed, (await e.Completion).Reason);
        Assert.Equal(1, probe.Reads);
        Assert.True(probe.ReadToken.CanBeCanceled);
        Assert.Equal((1, 1, 1), (probe.ReaderDisposals, probe.CommandDisposals, probe.ConnectionDisposals));
    }
    [Fact(DisplayName = "[UC-010] disposal begun synchronously inside a mapper waits for the tracked advance")]
    public async Task Synchronous_mapping_cannot_be_disposed_before_its_advance_settles()
    {
        var probe = new StreamProbe();
        QueryStreamEnumerator<StreamRow>? e = null;
        Task? disposal = null;
        var stream = probe.Query(map: reader =>
        {
            disposal = e!.DisposeAsync().AsTask();
            Assert.False(disposal.IsCompleted);
            return new StreamRow(reader.GetInt32(0), "detached");
        }).StreamAsync();
        e = stream.GetAsyncEnumerator();
        Assert.False(await e.MoveNextAsync());
        await disposal!;
        Assert.Equal(QueryStreamStopReason.Disposed, (await e.Completion).Reason);
        Assert.Equal(0, (await e.Completion).RowsYielded);
        Assert.Equal((1, 1, 1), (probe.ReaderDisposals, probe.CommandDisposals, probe.ConnectionDisposals));
    }
    [Fact(DisplayName = "[UC-010] enumeration cancellation reaches ReadAsync and isolates terminal token")]
    public async Task Enumeration_token_cancels_real_inflight_driver_await()
    {
        using var terminal = new CancellationTokenSource();
        using var enumeration = new CancellationTokenSource();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var probe = new StreamProbe { BeforeRead = ct =>
        { reached.TrySetResult(); return Task.Delay(Timeout.Infinite, ct); } };
        await using var e = probe.Query().StreamAsync(terminal.Token).GetAsyncEnumerator(enumeration.Token);
        var advance = e.MoveNextAsync().AsTask();
        await reached.Task;
        enumeration.Cancel();
        var error = await Assert.ThrowsAsync<QueryStreamCanceledException>(() => advance);
        Assert.Equal(enumeration.Token, error.CancellationToken);
        Assert.False(terminal.IsCancellationRequested);
        Assert.True((await e.Completion).CleanupSucceeded);
        Assert.Equal(1, probe.Reads);
        Assert.Equal((1, 1, 1), (probe.ReaderDisposals, probe.CommandDisposals, probe.ConnectionDisposals));
    }
    [Fact(DisplayName = "[UC-010] Completion waits for private-cancellation callbacks and retains their failures")]
    public async Task Cancel_callback_failure_cannot_arrive_after_published_completion()
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackError = new IOException("private callback");
        var probe = new StreamProbe { BeforeRead = ct =>
        {
            ct.Register(() => throw callbackError);
            reached.TrySetResult();
            return Task.Delay(Timeout.Infinite, ct);
        } };
        var e = probe.Query().StreamAsync().GetAsyncEnumerator();
        var advance = e.MoveNextAsync().AsTask();
        await reached.Task;
        await e.DisposeAsync();
        Assert.False(await advance);
        var end = await e.Completion;
        Assert.Equal(QueryStreamStopReason.Disposed, end.Reason);
        Assert.False(end.CleanupSucceeded);
        var aggregate = Assert.IsType<AggregateException>(Assert.Single(end.CleanupErrors));
        Assert.Same(callbackError, Assert.Single(aggregate.InnerExceptions));
        Assert.DoesNotContain("private", end.ToString());
    }
}
