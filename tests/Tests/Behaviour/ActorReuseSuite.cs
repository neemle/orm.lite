using Xunit;

namespace Neemle.OrmLite.Tests.Behaviour;

public abstract class ActorReuseSuite<TFixture>(TFixture fixture) : IClassFixture<TFixture>
    where TFixture : ProviderFixture
{
    private OrmContext Ctx => fixture.Context;

    private string ActorSql => fixture.ProviderId == "sqlite"
        ? "SELECT coalesce(orm_actor(), '')"
        : $"SELECT coalesce(current_setting('{OrmLiteSettings.ActorSetting}', true), '')";

    [SkippableFact(DisplayName = "one data-source command run under an actor and then outside any actor reports the "
        + "actor the first time and no author the second, never the stale stamp")]
    public async Task A_reused_command_carries_no_stale_actor()
    {
        fixture.RequireAvailable();
        var user = Guid.NewGuid();
        await using var command = Ctx.DataSource.CreateCommand(ActorSql);

        string inside;
        using (DbActor.EnterUser(user)) inside = (string)(await command.ExecuteScalarAsync())!;
        var outside = (string)(await command.ExecuteScalarAsync())!;

        Assert.Equal(user.ToString("D"), inside);
        Assert.Equal("", outside);
    }

    [SkippableFact(DisplayName = "a command run under actor A and then under actor B reports each in turn")]
    public async Task A_reused_command_follows_each_actor()
    {
        fixture.RequireAvailable();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await using var command = Ctx.DataSource.CreateCommand(ActorSql);

        string one, two;
        using (DbActor.EnterUser(first)) one = (string)(await command.ExecuteScalarAsync())!;
        using (DbActor.EnterUser(second)) two = (string)(await command.ExecuteScalarAsync())!;

        Assert.Equal(first.ToString("D"), one);
        Assert.Equal(second.ToString("D"), two);
    }

    [SkippableFact(DisplayName = "a connection leased after an actor scope ended reports no author")]
    public async Task A_new_lease_after_an_actor_scope_has_no_actor()
    {
        fixture.RequireAvailable();
        using (DbActor.EnterUser(Guid.NewGuid()))
        {
            await using (await Ctx.DataSource.OpenConnectionAsync())
            {
            }
        }

        await using var connection = await Ctx.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = ActorSql;

        Assert.Equal("", (string)(await command.ExecuteScalarAsync())!);
    }
}
