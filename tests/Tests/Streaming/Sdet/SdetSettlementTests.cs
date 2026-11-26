using Xunit;

namespace Neemle.OrmLite.Tests.Streaming.Sdet;

public sealed class SdetSettlementTests
{
    [Fact(DisplayName = "[UC-010] user cancellation with an ignoring driver waits for settlement and preserves cleanup")]
    public async Task Cancellation_cannot_publish_completion_while_driver_still_owns_reader()
    {
        using var cancel = new CancellationTokenSource();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var p = new StreamProbe { IgnoreCancellation = true, BeforeRead = _ =>
        { reached.TrySetResult(); return release.Task; }, ReaderError = new IOException("cleanup") };
        var e = p.Query().StreamAsync(cancel.Token).GetAsyncEnumerator();
        var advance = e.MoveNextAsync().AsTask();
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancel.Cancel();
        var disposal = e.DisposeAsync().AsTask();
        try
        {
            Assert.False(advance.IsCompleted);
            Assert.False(disposal.IsCompleted);
            Assert.False(e.Completion.IsCompleted);
            Assert.Equal(0, p.ReaderDisposals);
        }
        finally { release.TrySetResult(); }
        var fault = await Assert.ThrowsAsync<QueryStreamCanceledException>(() => advance);
        await disposal;
        Assert.Equal(cancel.Token, fault.CancellationToken);
        var end = await e.Completion;
        Assert.Equal(QueryStreamStopReason.Canceled, end.Reason);
        Assert.False(end.CleanupSucceeded);
        Assert.Equal(0, end.RowsYielded);
        Assert.Equal((1, 1, 1), (p.ReaderDisposals, p.CommandDisposals, p.ConnectionDisposals));
    }

    [Fact(DisplayName = "[UC-010] advancing invalidates previous Current before awaited reader settles")]
    public async Task Pending_advance_has_no_stale_current_or_background_map()
    {
        var p = new StreamProbe();
        var e = p.Query().StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        var detached = e.Current;
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        p.BeforeRead = _ => { reached.TrySetResult(); return release.Task; };
        var advance = e.MoveNextAsync().AsTask();
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.Equal(QueryStreamError.InvalidState, Assert.Throws<QueryStreamException>(() => e.Current).Error);
            Assert.Equal((2, 1), (p.Reads, p.Maps));
            Assert.Equal(1, detached.Id);
        }
        finally { release.TrySetResult(); }
        Assert.True(await advance);
        Assert.Equal(2, e.Current.Id);
        await e.DisposeAsync();
        Assert.True((await e.Completion).CleanupSucceeded);
    }
}
