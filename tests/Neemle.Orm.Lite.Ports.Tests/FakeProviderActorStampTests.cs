using Xunit;

namespace Neemle.Orm.Lite.Ports.Tests;

// The core never writes the statement that stamps the acting user: it hands the actor to the provider, which owns the
// spelling. The fake provider records the hand-over as an event and runs no statement, so any SQL the core wrote
// itself would show up as a statement of the fake server.
public sealed class FakeProviderActorStampTests
{
    private readonly PortsScenario _scenario = new();

    private GatedDataSource Gate() => new(_scenario.Provider, _scenario.Source, _scenario.Source, _scenario.Source);

    [Fact(DisplayName = "a connection leased inside a user scope is stamped through the provider with that user, "
        + "and the core runs no statement of its own")]
    public async Task A_user_scope_is_stamped_through_the_provider()
    {
        var user = Guid.NewGuid();

        using (DbActor.EnterUser(user))
        {
            await using var connection = await Gate().OpenConnectionAsync();
        }

        Assert.Equal(new[] { "stamp:" + user.ToString("D") }, _scenario.Database.Events);
        Assert.Empty(_scenario.Database.Statements);
    }

    [Fact(DisplayName = "a connection leased inside a system scope is stamped through the provider as the system "
        + "actor")]
    public async Task A_system_scope_is_stamped_through_the_provider()
    {
        using (DbActor.EnterSystem())
        {
            await using var connection = await Gate().OpenConnectionAsync();
        }

        Assert.Equal(new[] { "stamp:system" }, _scenario.Database.Events);
        Assert.Empty(_scenario.Database.Statements);
    }

    [Fact(DisplayName = "a connection leased outside any actor scope is stamped with nothing: the provider's clear "
        + "hook runs and the core writes no statement")]
    public async Task No_scope_stamps_nothing()
    {
        await using var connection = await Gate().OpenConnectionAsync();

        Assert.Empty(_scenario.Database.Events);
        Assert.Empty(_scenario.Database.Statements);
    }
}
