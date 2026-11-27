using Neemle.OrmLite.Tests.Streaming;
using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

[Collection("Graph")]
public sealed class OrphanGuardSessionContractTests
{
    [Fact]
    public async Task Command_binds_exact_resources_and_values_without_taking_caller_ownership()
    {
        var fixture = new OrphanGuardSessionFixture();
        fixture.Probe.Executing = command =>
        {
            Assert.Same(fixture.Connection, command.Connection);
            Assert.Same(fixture.Transaction, command.Transaction);
        };
        var session = fixture.Session();
        Assert.Equal(new[] { 1, 2, 3 }, await fixture.Read(session));
        Assert.Equal(new object?[] { 7 }, fixture.Probe.BoundValues);
        Assert.Equal(1, session.StatementsUsed);
        fixture.AssertCleanup();
    }

    [Fact]
    public async Task Statement_budget_is_cumulative_and_refuses_before_next_command_creation()
    {
        var fixture = new OrphanGuardSessionFixture();
        var session = fixture.Session(budget: 1);
        Assert.Equal(3, (await fixture.Read(session)).Count);
        var error = await Assert.ThrowsAsync<OrphanException>(() => fixture.Read(session));
        Assert.Equal(OrphanError.LimitExceeded, error.Error);
        Assert.Equal(OrphanOutcome.NotStarted, error.Outcome);
        Assert.Equal(1, session.StatementsUsed);
        Assert.Equal(1, fixture.Probe.Commands);
        Assert.Equal(1, fixture.Probe.Executions);
        fixture.AssertCleanup();
    }

    [Fact]
    public async Task Already_requested_token_cancels_before_command_or_budget_reservation()
    {
        var fixture = new OrphanGuardSessionFixture();
        var session = fixture.Session();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.Read(session, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal(0, fixture.Probe.Commands);
        Assert.Equal(0, session.StatementsUsed);
        fixture.AssertCallerOwned();
    }

    [Fact]
    public async Task Current_closed_connection_refuses_without_creating_a_command()
    {
        var fixture = new OrphanGuardSessionFixture();
        var session = fixture.Session();
        fixture.Connection.Close();
        var error = await Assert.ThrowsAsync<OrphanException>(() => fixture.Read(session));
        Assert.Equal(OrphanError.ScopeEnded, error.Error);
        Assert.Equal(0, fixture.Probe.Commands);
        Assert.Equal(0, session.StatementsUsed);
        Assert.Equal(0, fixture.Transaction.Commits + fixture.Transaction.Rollbacks + fixture.Transaction.Disposals);
    }

    [Fact]
    public void Foreign_transaction_connection_refuses_before_any_command()
    {
        var fixture = new OrphanGuardSessionFixture();
        var foreign = new OrphanGuardProbeTransaction(new StreamProbeConnection(new StreamProbe()));
        var error = Assert.Throws<OrphanException>(() => fixture.Session(transaction: foreign));
        Assert.Equal(OrphanError.ScopeEnded, error.Error);
        Assert.Equal(0, fixture.Probe.Commands);
        Assert.Equal(0, foreign.Commits + foreign.Rollbacks + foreign.Disposals);
        fixture.AssertCallerOwned();
    }

    [Fact]
    public async Task Ending_session_is_idempotent_and_never_settles_caller_resources()
    {
        var fixture = new OrphanGuardSessionFixture();
        var session = fixture.Session();
        session.End();
        session.End();
        var error = await Assert.ThrowsAsync<OrphanException>(() => fixture.Read(session));
        Assert.Equal(OrphanError.ScopeEnded, error.Error);
        Assert.Equal(0, fixture.Probe.Commands);
        Assert.Equal(0, session.StatementsUsed);
        fixture.AssertCallerOwned();
    }
}
