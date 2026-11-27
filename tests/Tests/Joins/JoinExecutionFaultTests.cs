using Xunit;
namespace Neemle.OrmLite.Tests.Joins;
[Collection("Graph")]
public sealed class JoinExecutionFaultTests
{
    [Fact] public async Task Open_fault_preserves_cause_without_command()
    {
        var f = new JoinExecutionFixture(); var primary = new InvalidOperationException("private-open");
        f.Probe.OpenError = primary; await Failure(f, primary, JoinError.ProviderFailure); f.Owned(0, 0);
    }
    [Fact] public async Task Execute_fault_disposes_command_and_owned_connection()
    {
        var f = new JoinExecutionFixture(); var primary = new InvalidOperationException("private-execute");
        f.Probe.ExecuteError = primary; await Failure(f, primary, JoinError.ProviderFailure); f.Owned(0);
    }
    [Fact] public async Task Mid_read_fault_returns_no_successful_prefix()
    {
        var f = new JoinExecutionFixture(); var primary = new InvalidOperationException("private-read");
        f.Probe.ReadError = primary; f.Probe.ReadFaultAt = 2;
        await Failure(f, primary, JoinError.ProviderFailure); Assert.Equal(2, f.Probe.Decodes); f.Owned();
    }
    [Fact] public async Task Second_row_decoder_fault_returns_no_successful_prefix()
    {
        var f = new JoinExecutionFixture(); f.Probe.Rows = [f.Probe.Rows[0], f.Probe.Rows[0]];
        var primary = new InvalidOperationException("private-decoder");
        f.Probe.DecodeError = primary; f.Probe.DecodeFaultAt = 3;
        await Failure(f, primary, JoinError.MappingFailure); Assert.Equal(3, f.Probe.Decodes); f.Owned();
    }
    [Theory] [InlineData("open")] [InlineData("execute")] [InlineData("read")]
    public async Task Actual_phase_cancellation_preserves_token_and_cleans_owned_objects(string phase)
    {
        var f = new JoinExecutionFixture(); using var token = new CancellationTokenSource();
        Task Cancel(CancellationToken _) { token.Cancel(); return Task.CompletedTask; }
        if (phase == "open") f.Probe.Opening = Cancel;
        if (phase == "execute") f.Probe.Executing = Cancel;
        if (phase == "read") f.Probe.Reading = Cancel;
        var error = await Assert.ThrowsAsync<JoinReadCanceledException>(() => f.Run(token.Token));
        Assert.Equal(token.Token, error.CancellationToken);
        var original = Assert.IsAssignableFrom<OperationCanceledException>(error.InnerException);
        Assert.Equal(token.Token, original.CancellationToken);
        Assert.Equal(token.Token, f.Probe.OpenToken);
        if (phase != "open") Assert.Equal(token.Token, f.Probe.ExecuteToken);
        if (phase == "read") Assert.Equal(token.Token, f.Probe.ReadToken);
        f.Owned(phase == "read" ? 1 : 0, phase == "open" ? 0 : 1);
    }
    [Fact] public async Task Unrequested_native_cancellation_is_provider_failure()
    {
        var f = new JoinExecutionFixture(); var primary = new OperationCanceledException("private-unrequested");
        f.Probe.ReadError = primary; f.Probe.ReadFaultAt = 1;
        await Failure(f, primary, JoinError.ProviderFailure); f.Owned();
    }
    [Fact] public async Task Late_token_does_not_relabel_observed_provider_primary()
    {
        var f = new JoinExecutionFixture(); using var token = new CancellationTokenSource();
        var primary = new InvalidOperationException("private-primary"); f.Probe.ExecuteError = primary;
        f.Probe.Executing = _ => { token.Cancel(); return Task.CompletedTask; };
        var error = await Assert.ThrowsAsync<JoinReadException>(() => f.Run(token.Token));
        Assert.Equal(JoinError.ProviderFailure, error.Error); Assert.Same(primary, error.InnerException); f.Owned(0);
    }
    [Fact] public async Task Primary_survives_all_three_cleanup_failures_and_all_attempts_happen()
    {
        var f = new JoinExecutionFixture(); var primary = new InvalidOperationException("private-primary");
        f.Probe.ReadError = primary; f.Probe.ReadFaultAt = 1; CleanupFaults(f);
        await Failure(f, primary, JoinError.ProviderFailure); f.Owned();
        Assert.Equal(new[] { "reader", "command", "connection" }, f.Probe.Events);
    }
    [Fact] public async Task Cleanup_only_failure_returns_no_list_and_keeps_first_cleanup_cause()
    {
        var f = new JoinExecutionFixture(); CleanupFaults(f);
        await Failure(f, f.Probe.ReaderError!, JoinError.ProviderFailure); f.Owned();
        Assert.Equal(new[] { "reader", "command", "connection" }, f.Probe.Events);
    }
    [Fact] public async Task Borrowed_read_failure_leaves_connection_transaction_with_caller()
    {
        var f = new JoinExecutionFixture(); f.Bind(); var primary = new InvalidOperationException("private-borrowed");
        f.Probe.ReadError = primary; f.Probe.ReadFaultAt = 2;
        await Failure(f, primary, JoinError.ProviderFailure); f.Borrowed();
        Assert.Equal(1, f.Probe.ReaderDisposals); Assert.Equal(1, f.Probe.CommandDisposals);
    }
    [Fact] public async Task Overflow_reads_only_bound_plus_one_and_never_returns_prefix()
    {
        var f = new JoinExecutionFixture(); f.Probe.Rows = [f.Probe.Rows[0], f.Probe.Rows[0], f.Probe.Rows[0]];
        var error = await Assert.ThrowsAsync<JoinReadException>(() =>
            f.Query(new(4, 16, 128, 64, 2)).ToListAsync(f.Model.Selection));
        Assert.Equal(JoinError.LimitExceeded, error.Error); Assert.Equal(3, f.Probe.Reads);
        Assert.Equal(4, f.Probe.Decodes); f.Owned();
    }
    private static async Task Failure(JoinExecutionFixture f, Exception primary, JoinError category)
    {
        var error = await Assert.ThrowsAsync<JoinReadException>(() => f.Run());
        Assert.Equal(category, error.Error); Assert.Same(primary, error.InnerException);
        Assert.DoesNotContain(primary.Message, error.Message); Assert.DoesNotContain(primary.Message, error.ToString());
    }
    private static void CleanupFaults(JoinExecutionFixture f)
    {
        f.Probe.ReaderError = new InvalidOperationException("private-reader-cleanup");
        f.Probe.CommandError = new InvalidOperationException("private-command-cleanup");
        f.Probe.ConnectionError = new InvalidOperationException("private-connection-cleanup");
    }
}
