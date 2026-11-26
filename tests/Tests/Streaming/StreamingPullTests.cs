using Xunit;

namespace Neemle.OrmLite.Tests.Streaming;

public sealed class StreamingPullTests
{
    [Fact(DisplayName = "[UC-010] cold terminal and unused enumerator allocate zero database resources")]
    public async Task Cold_enumerator_disposes_without_starting_io()
    {
        var probe = new StreamProbe();
        var stream = probe.Query().StreamAsync();
        var e = stream.GetAsyncEnumerator();
        Assert.Equal(0, probe.Connections);
        Assert.False(e.Completion.IsCompleted);
        Assert.Equal(QueryStreamError.InvalidState, Assert.Throws<QueryStreamException>(() => e.Current).Error);
        await e.DisposeAsync();
        var end = await e.Completion;
        Assert.Equal(QueryStreamStopReason.Disposed, end.Reason);
        Assert.True(end.CleanupSucceeded);
        Assert.Equal(0, probe.Connections);
        Assert.False(await e.MoveNextAsync());
    }

    [Fact(DisplayName = "[UC-010] one demand maps one row and early disposal releases ownership")]
    public async Task Pull_has_no_row_history_or_read_ahead()
    {
        var probe = new StreamProbe { Count = 1000000 };
        await using var e = probe.Query().StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        var first = e.Current;
        await Task.Yield();
        Assert.Equal((1, 1, 1), (probe.Executions, probe.Reads, probe.Maps));
        Assert.True(await e.MoveNextAsync());
        Assert.Equal(2, e.Current.Id);
        await e.DisposeAsync();
        Assert.Equal(new StreamRow(1, "row-1"), first);
        Assert.Equal((2, 2), (probe.Reads, probe.Maps));
        Assert.Equal((1, 1, 1), (probe.ReaderDisposals, probe.CommandDisposals, probe.ConnectionDisposals));
        Assert.Equal(QueryStreamStopReason.Disposed, (await e.Completion).Reason);
        Assert.Equal(2, (await e.Completion).RowsYielded);
    }

    [Theory(DisplayName = "[UC-010] EOF completes zero/one/many rows after one end read and exact cleanup")]
    [InlineData(0)] [InlineData(1)] [InlineData(4)]
    public async Task Clean_eof_is_completed_and_repeat_advance_does_no_io(int count)
    {
        var probe = new StreamProbe { Count = count };
        await using var e = probe.Query().StreamAsync().GetAsyncEnumerator();
        var values = new List<int>();
        while (await e.MoveNextAsync()) values.Add(e.Current.Id);
        Assert.Equal(Enumerable.Range(1, count), values);
        Assert.Equal(count + 1, probe.Reads);
        Assert.Equal(count, probe.Maps);
        Assert.Equal((1, 1, 1), (probe.ReaderDisposals, probe.CommandDisposals, probe.ConnectionDisposals));
        var end = await e.Completion;
        Assert.Equal(QueryStreamStopReason.Completed, end.Reason);
        Assert.True(end.CleanupSucceeded);
        Assert.False(await e.MoveNextAsync());
        Assert.Equal(count + 1, probe.Reads);
    }

    [Fact(DisplayName = "[UC-010] stream capture freezes the query and reuse has a fresh cursor")]
    public async Task Later_query_changes_do_not_mutate_the_stream()
    {
        var probe = new StreamProbe();
        var query = probe.Query().Where(r => r.Id > 1).OrderBy(r => r.Id).Take(5);
        var expected = query.CompileSelect();
        var stream = query.StreamAsync();
        query.Where(r => r.Id > 999).Take(1);
        for (var i = 0; i < 2; i++)
        {
            await using var e = stream.GetAsyncEnumerator();
            Assert.True(await e.MoveNextAsync());
            Assert.Equal(expected.Text, probe.Sql);
            Assert.Equal(expected.Values, probe.BoundValues);
        }
        Assert.Equal((2, 2), (probe.Connections, probe.Executions));
        Assert.NotEqual(expected.Text, query.CompileSelect().Text);
    }
}
