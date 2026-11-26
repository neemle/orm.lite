using Xunit;

namespace Neemle.OrmLite.Tests.Streaming.Sdet;

public sealed class SdetPrimaryCleanupTests
{
    [Theory(DisplayName = "[UC-010][SEC] independent primary survives every acquired cleanup fault")]
    [InlineData("execute")] [InlineData("read")] [InlineData("map")]
    public async Task Primary_and_all_cleanup_failures_remain_inspectable(string phase)
    {
        var primary = new IOException("private-row-and-credential-primary");
        var reader = new IOException("private-reader");
        var command = new IOException("private-command");
        var connection = new IOException("private-connection");
        var p = new StreamProbe { ReaderError = reader, CommandError = command, ConnectionError = connection };
        if (phase == "execute") p.ExecuteError = primary;
        if (phase == "read") { p.ReadFaultAt = 2; p.ReadError = primary; }
        var q = phase == "map" ? p.Query(map: _ => throw primary) : p.Query();
        var e = q.StreamAsync().GetAsyncEnumerator();
        if (phase == "read") Assert.True(await e.MoveNextAsync());
        var fault = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        var end = await e.Completion;
        Assert.Same(primary, fault.InnerException);
        Assert.Same(primary, end.PrimaryError);
        Assert.Equal(phase == "map" ? QueryStreamError.MappingFailure : QueryStreamError.ProviderFailure, fault.Error);
        Assert.Equal(phase == "execute" ? new[] { command, connection } : new[] { reader, command, connection },
            end.CleanupErrors);
        Assert.Equal(phase == "read" ? 1 : 0, end.RowsYielded);
        Assert.Equal(QueryStreamStopReason.Faulted, end.Reason);
        Assert.DoesNotContain("private", fault.ToString() + end);
        await e.DisposeAsync();
        Assert.Same(end, await e.Completion);
        Assert.Equal((phase == "execute" ? 0 : 1, 1, 1),
            (p.ReaderDisposals, p.CommandDisposals, p.ConnectionDisposals));
    }

    [Fact(DisplayName = "[UC-010] EOF command/connection faults cannot masquerade as clean completion")]
    public async Task Empty_eof_attempts_both_failing_cleanup_stages()
    {
        var command = new IOException("command");
        var connection = new IOException("connection");
        var p = new StreamProbe { Count = 0, CommandError = command, ConnectionError = connection };
        var e = p.Query().StreamAsync().GetAsyncEnumerator();
        var fault = await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask());
        Assert.Equal(QueryStreamError.CleanupFailure, fault.Error);
        var end = await e.Completion;
        Assert.Equal(QueryStreamStopReason.Faulted, end.Reason);
        Assert.Null(end.PrimaryError);
        Assert.Equal(new[] { command, connection }, end.CleanupErrors);
        Assert.Same(fault, await Assert.ThrowsAsync<QueryStreamException>(() => e.MoveNextAsync().AsTask()));
        await e.DisposeAsync();
        Assert.Equal((1, 1, 1, 1), (p.Reads, p.ReaderDisposals, p.CommandDisposals, p.ConnectionDisposals));
    }
}
