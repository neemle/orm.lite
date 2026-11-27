using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanGuardSessionFaultTests
{
    [Fact]
    public async Task Reader_execution_fault_keeps_primary_and_disposes_command_only()
    {
        var fixture = new OrphanGuardSessionFixture();
        var primary = new InvalidOperationException("execute-primary");
        fixture.Probe.ExecuteError = primary;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Read(fixture.Session()));
        Assert.Same(primary, error);
        Assert.Equal(1, fixture.Probe.Executions);
        Assert.Equal(0, fixture.Probe.Reads);
        fixture.AssertCleanup(readers: 0);
    }

    [Fact]
    public async Task Mid_read_fault_returns_no_prefix_and_disposes_both_owned_objects()
    {
        var fixture = new OrphanGuardSessionFixture();
        var primary = new InvalidOperationException("read-primary");
        fixture.Probe.ReadError = primary;
        fixture.Probe.ReadFaultAt = 2;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Read(fixture.Session()));
        Assert.Same(primary, error);
        Assert.Equal(2, fixture.Probe.Reads);
        Assert.Equal(1, fixture.Probe.Maps);
        fixture.AssertCleanup();
    }

    [Fact]
    public async Task Mapping_fault_keeps_original_exception_and_disposes_reader_then_command()
    {
        var fixture = new OrphanGuardSessionFixture();
        var primary = new InvalidOperationException("map-primary");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Read(fixture.Session(), map: _ => throw primary));
        Assert.Same(primary, error);
        Assert.Equal(1, fixture.Probe.Reads);
        fixture.AssertCleanup();
    }

    [Fact]
    public async Task Cancellation_during_read_preserves_actual_token_and_owned_cleanup()
    {
        var fixture = new OrphanGuardSessionFixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Probe.BeforeRead = _ => { cancellation.Cancel(); return Task.CompletedTask; };
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Read(fixture.Session(), cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(cancellation.Token, fixture.Probe.ReadToken);
        Assert.Equal(0, fixture.Probe.Maps);
        fixture.AssertCleanup();
    }

    [Fact]
    public async Task Primary_read_fault_survives_two_cleanup_faults_and_both_attempts_happen()
    {
        var fixture = new OrphanGuardSessionFixture();
        var primary = new InvalidOperationException("read-primary");
        fixture.Probe.ReadError = primary;
        fixture.Probe.ReadFaultAt = 1;
        fixture.Probe.ReaderError = new InvalidOperationException("reader-cleanup");
        fixture.Probe.CommandError = new InvalidOperationException("command-cleanup");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Read(fixture.Session()));
        Assert.Same(primary, error);
        fixture.AssertCleanup();
    }

    [Fact]
    public async Task Cleanup_only_fault_returns_no_list_and_keeps_first_reader_cleanup_failure()
    {
        var fixture = new OrphanGuardSessionFixture();
        var first = new InvalidOperationException("reader-cleanup");
        fixture.Probe.ReaderError = first;
        fixture.Probe.CommandError = new InvalidOperationException("command-cleanup");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Read(fixture.Session()));
        Assert.Same(first, error);
        Assert.Equal(3, fixture.Probe.Maps);
        fixture.AssertCleanup();
    }
}
