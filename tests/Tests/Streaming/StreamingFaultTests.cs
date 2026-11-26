using Xunit;

namespace Neemle.OrmLite.Tests.Streaming;

public sealed class StreamingFaultTests
{
    [Fact(DisplayName = "[UC-010] EOF cleanup failure is Faulted, throws and preserves the valid consumed prefix")]
    public async Task Eof_never_reports_success_when_cleanup_failed()
    {
        var cleanup = new IOException("private cleanup value");
        var probe = new StreamProbe { Count = 1, ReaderError = cleanup };
        await using var e = probe.Query().StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        var first = e.Current;
        var error = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.CleanupFailure, error.Error);
        Assert.Equal(1, error.RowsYielded);
        Assert.Same(error, await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask()));
        Assert.Equal(QueryStreamStopReason.Faulted, (await e.Completion).Reason);
        Assert.Same(cleanup, Assert.Single((await e.Completion).CleanupErrors));
        Assert.Equal((1, 1, 1), (probe.ReaderDisposals, probe.CommandDisposals, probe.ConnectionDisposals));
        Assert.DoesNotContain("private", error.ToString());
        Assert.Equal(1, first.Id);
    }

    [Fact(DisplayName = "[SEC][UC-010] early cleanup attempts all resources without replacing a consumer body error")]
    public async Task Body_failure_wins_and_completion_exposes_cleanup_faults()
    {
        var body = new InvalidOperationException("consumer body");
        var one = new IOException("reader password=secret");
        var two = new IOException("command password=secret");
        var three = new IOException("connection password=secret");
        var probe = new StreamProbe { ReaderError = one, CommandError = two, ConnectionError = three };
        var e = probe.Query().StreamAsync().GetAsyncEnumerator();
        var observed = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            try { Assert.True(await e.MoveNextAsync()); throw body; }
            finally { await e.DisposeAsync(); }
        });
        Assert.Same(body, observed);
        var end = await e.Completion;
        Assert.Equal(QueryStreamStopReason.Disposed, end.Reason);
        Assert.False(end.CleanupSucceeded);
        Assert.Equal(new[] { one, two, three }, end.CleanupErrors);
        Assert.DoesNotContain("secret", end.ToString());
        Assert.Equal((1, 1, 1), (probe.ReaderDisposals, probe.CommandDisposals, probe.ConnectionDisposals));
    }

    [Fact(DisplayName = "[UC-010] a late mapper error preserves its original cause, prefix and cached failure")]
    public async Task Late_map_failure_cannot_become_false_eof_or_replay()
    {
        var primary = new InvalidOperationException("password=private-map");
        var probe = new StreamProbe();
        var query = probe.Query(map: r => ++probe.Maps == 2 ? throw primary : new StreamRow(r.GetInt32(0), "detached"));
        await using var e = query.StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        var first = e.Current;
        var error = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.MappingFailure, error.Error);
        Assert.Same(primary, error.InnerException);
        Assert.Same(primary, (await e.Completion).PrimaryError);
        Assert.Equal(1, error.RowsYielded);
        Assert.Equal(1, first.Id);
        Assert.DoesNotContain("private-map", error.ToString());
        Assert.Equal(1, probe.Executions);
    }

    [Theory(DisplayName = "[UC-010] partial gated-open faults retain original primary and every cleanup failure")]
    [InlineData(true)] [InlineData(false)]
    public async Task Gated_partial_open_preserves_primary_across_close_and_dispose_faults(bool actor)
    {
        var primary = new IOException("private-open");
        var close = new IOException("private-close");
        var dispose = new IOException("private-dispose");
        var probe = new StreamProbe { CloseError = close, ConnectionError = dispose };
        if (actor) probe.ActorError = primary; else probe.OpenError = primary;
        using var lane = DbLane.Enter(DbLaneKind.Request);
        await using var e = probe.Query(true).StreamAsync().GetAsyncEnumerator();
        var error = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        Assert.Same(primary, error.InnerException);
        Assert.Equal(new[] { close, dispose }, error.CleanupErrors);
        Assert.Same(primary, (await e.Completion).PrimaryError);
        Assert.False((await e.Completion).CleanupSucceeded);
        Assert.Equal((1, 1, 0), (probe.Closes, probe.ConnectionDisposals, probe.Commands));
        Assert.DoesNotContain("private", error.ToString());
        Assert.Equal(actor ? 1 : 0, lane.HeldLeases);
    }
    [Theory(DisplayName = "[UC-010] execute/read faults retain primary and clean acquired resources")]
    [InlineData(true)] [InlineData(false)]
    public async Task Provider_fault_preserves_exact_cause_and_prefix(bool execute)
    {
        var primary = new IOException("password=private-provider");
        var probe = new StreamProbe();
        if (execute) probe.ExecuteError = primary;
        else { probe.ReadFaultAt = 2; probe.ReadError = primary; }
        await using var e = probe.Query().StreamAsync().GetAsyncEnumerator();
        if (!execute) Assert.True(await e.MoveNextAsync());
        var error = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.ProviderFailure, error.Error);
        Assert.Equal(execute ? 0 : 1, error.RowsYielded);
        Assert.Same(primary, error.InnerException);
        Assert.Same(primary, (await e.Completion).PrimaryError);
        Assert.Equal(1, probe.CommandDisposals);
        Assert.Equal(1, probe.ConnectionDisposals);
        Assert.Equal(execute ? 0 : 1, probe.ReaderDisposals);
    }

    [Fact(DisplayName = "[UC-010] unrelated mapper cancellation is MappingFailure rather than user cancellation")]
    public async Task Mapper_oce_does_not_fabricate_a_canceled_token()
    {
        var primary = new OperationCanceledException("private mapper");
        var probe = new StreamProbe();
        await using var e = probe.Query(map: _ => throw primary).StreamAsync().GetAsyncEnumerator();
        var error = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.MappingFailure, error.Error);
        Assert.Same(primary, error.InnerException);
        Assert.Equal(QueryStreamStopReason.Faulted, (await e.Completion).Reason);
    }

}
