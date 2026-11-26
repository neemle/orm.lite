using Neemle.OrmLite.Tests.Streaming.Sdet;
using Xunit;

namespace Neemle.OrmLite.Tests.Streaming.Final;

public sealed class FinalDiagnosticTests
{
    [Fact(DisplayName = "[UC-010][SEC02] a sink alone does not enable diagnostics on late read failure")]
    public async Task Default_off_sink_sees_no_sensitive_failure()
    {
        var p = new StreamProbe { ReadFaultAt = 2, ReadError = new IOException("password=late-private") };
        var sink = new SdetStatementSink();
        await using var context = FinalQueries.Context(p, new OrmLiteDiagnosticsOptions { Sink = sink });
        var e = FinalQueries.Query(context).StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        await e.DisposeAsync();
        Assert.Empty(sink.Statements);
        Assert.Empty(sink.Transactions);
        Assert.Equal(1, (await e.Completion).RowsYielded);
        Assert.Equal(1, p.Executions);
    }

    [Fact(DisplayName = "[UC-010][SEC02] execute event already exists while first provider read is pending")]
    public async Task Execute_elapsed_does_not_wait_for_consumption()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var p = new StreamProbe { BeforeRead = async ct => { entered.SetResult(); await release.Task.WaitAsync(ct); } };
        var sink = new SdetStatementSink();
        var options = new OrmLiteDiagnosticsOptions { Sink = sink, StatementLevel = OrmLogLevel.Information };
        await using var context = FinalQueries.Context(p, options);
        var e = FinalQueries.Query(context).StreamAsync().GetAsyncEnumerator();
        var moving = e.MoveNextAsync().AsTask();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var execution = Assert.Single(sink.Statements);
        var elapsed = execution.Elapsed;
        Assert.False(moving.IsCompleted);
        release.SetResult();
        Assert.True(await moving);
        await e.DisposeAsync();
        Assert.Same(execution, Assert.Single(sink.Statements));
        Assert.Equal(elapsed, execution.Elapsed);
        Assert.Null(execution.RowsAffected);
        Assert.True((await e.Completion).CleanupSucceeded);
    }

    [Theory(DisplayName = "[UC-010][SEC02] late provider/EOF cleanup faults add no automatic sensitive event")]
    [InlineData(false)] [InlineData(true)]
    public async Task Sensitive_late_faults_are_inspectable_without_duplicate_logging(bool eof)
    {
        var primary = new IOException("password=private-primary");
        var p = new StreamProbe { Count = 1, ReadFaultAt = eof ? 0 : 2, ReadError = primary,
            ReaderError = new IOException("password=private-reader"),
            CommandError = new IOException("password=private-command"),
            ConnectionError = new IOException("password=private-connection") };
        var sink = new SdetStatementSink();
        var options = new OrmLiteDiagnosticsOptions { Sink = sink, StatementLevel = OrmLogLevel.Information };
        await using var context = FinalQueries.Context(p, options);
        var e = FinalQueries.Query(context).StreamAsync().GetAsyncEnumerator();
        Assert.True(await e.MoveNextAsync());
        var prefix = e.Current;
        var failure = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        await e.DisposeAsync();
        var end = await e.Completion;
        Assert.Equal(eof ? QueryStreamError.CleanupFailure : QueryStreamError.ProviderFailure, failure.Error);
        Assert.Same(eof ? null : primary, end.PrimaryError);
        Assert.Equal(new[] { p.ReaderError, p.CommandError, p.ConnectionError }, end.CleanupErrors);
        Assert.Equal((1L, 1, 1, 1), (end.RowsYielded, p.Executions, p.ReaderDisposals, p.CommandDisposals));
        Assert.Equal(1, p.ConnectionDisposals);
        Assert.Equal(QueryStreamStopReason.Faulted, end.Reason);
        var execution = Assert.Single(sink.Statements);
        Assert.Null(execution.Error);
        Assert.DoesNotContain("private", failure.ToString() + end + execution);
        Assert.Equal(1, prefix.Id);
    }
}
