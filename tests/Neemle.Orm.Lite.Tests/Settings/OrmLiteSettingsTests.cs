using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Settings;

[Collection(ActorSettingCollection.Name)]
public sealed class OrmLiteSettingsTests
{
    [Fact(DisplayName = "OrmLiteSettings reads the library default when nothing is registered")]
    public void Nothing_registered_reads_the_default()
    {
        using var scope = ActorSettingScope.Fresh();

        Assert.Equal(OrmLiteSettings.DefaultActorSetting, OrmLiteSettings.ActorSetting);
    }

    [Fact(DisplayName = "OrmLiteSettings reads the registered name and builds the stamp statement from it")]
    public void A_registered_name_is_read_back()
    {
        using var scope = ActorSettingScope.Fresh();

        OrmLiteSettings.UseActorSetting("probe.actor");

        Assert.Equal("probe.actor", OrmLiteSettings.ActorSetting);
        Assert.Equal("SELECT set_config('probe.actor', @v, false)", OrmLiteSettings.StampStatement);
        Assert.Equal(OrmLiteSettings.StampStatement, DbActor.StampStatement);
    }

    [Fact(DisplayName = "OrmLiteSettings accepts the same name registered twice")]
    public void The_same_name_twice_is_idempotent()
    {
        using var scope = ActorSettingScope.Fresh();

        OrmLiteSettings.UseActorSetting("probe.actor");
        OrmLiteSettings.UseActorSetting("probe.actor");

        Assert.Equal("probe.actor", OrmLiteSettings.ActorSetting);
    }

    [Fact(DisplayName = "OrmLiteSettings refuses a different name after registration and names both")]
    public void A_different_name_after_registration_is_refused_naming_both()
    {
        using var scope = ActorSettingScope.Fresh();
        OrmLiteSettings.UseActorSetting("first.actor");

        var refusal = Assert.Throws<InvalidOperationException>(() => OrmLiteSettings.UseActorSetting("second.actor"));

        Assert.Contains("first.actor", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("second.actor", refusal.Message, StringComparison.Ordinal);
        Assert.Equal("first.actor", OrmLiteSettings.ActorSetting);
    }

    [Fact(DisplayName = "OrmLiteSettings refuses a name registered after the setting was first read and names both")]
    public void A_name_after_the_first_read_is_refused_naming_both()
    {
        using var scope = ActorSettingScope.Fresh();
        var frozen = OrmLiteSettings.ActorSetting;

        var refusal = Assert.Throws<InvalidOperationException>(() => OrmLiteSettings.UseActorSetting("late.actor"));

        Assert.Equal(OrmLiteSettings.DefaultActorSetting, frozen);
        Assert.Contains(frozen, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("late.actor", refusal.Message, StringComparison.Ordinal);
        Assert.Equal(frozen, OrmLiteSettings.ActorSetting);
    }

    [Fact(DisplayName = "OrmLiteSettings accepts the frozen name registered again after the first read")]
    public void The_frozen_name_registered_after_the_read_is_accepted()
    {
        using var scope = ActorSettingScope.Fresh();
        var frozen = OrmLiteSettings.ActorSetting;

        OrmLiteSettings.UseActorSetting(frozen);

        Assert.Equal(frozen, OrmLiteSettings.ActorSetting);
    }

    [Theory(DisplayName = "OrmLiteSettings refuses a name that is not a lowercase prefix, one dot and a lowercase name")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Probe.actor")]
    [InlineData("probe.Actor")]
    [InlineData("probeactor")]
    [InlineData("probe.actor.extra")]
    [InlineData(".actor")]
    [InlineData("probe.")]
    [InlineData("1probe.actor")]
    [InlineData("probe.1actor")]
    [InlineData("pro-be.actor")]
    [InlineData("probe.ac'tor")]
    [InlineData("probe.ac tor")]
    [InlineData("probe.actor\n")]
    [InlineData("abcdefghijklmnopqrstuvwxyz0123456.actor")]
    [InlineData("probe.abcdefghijklmnopqrstuvwxyz0123456")]
    public void An_invalid_name_is_refused_and_registers_nothing(string? name)
    {
        using var scope = ActorSettingScope.Fresh();

        Assert.Throws<ArgumentException>(() => OrmLiteSettings.UseActorSetting(name!));

        Assert.Equal(OrmLiteSettings.DefaultActorSetting, OrmLiteSettings.ActorSetting);
    }

    [Theory(DisplayName = "OrmLiteSettings accepts a name at the length and character limits")]
    [InlineData("a.b")]
    [InlineData("a_1.b_2")]
    [InlineData("abcdefghijklmnopqrstuvwxyz01234.abcdefghijklmnopqrstuvwxyz01234")]
    public void A_name_at_the_limits_is_accepted(string name)
    {
        using var scope = ActorSettingScope.Fresh();

        OrmLiteSettings.UseActorSetting(name);

        Assert.Equal(name, OrmLiteSettings.ActorSetting);
    }
}
