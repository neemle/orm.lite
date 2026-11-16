using Neemle.Orm.Lite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Settings;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ActorSettingCollection
{
    public const string Name = "OrmLiteActorSetting";
}

internal sealed class ActorSettingScope : IDisposable
{
    private readonly string _hostValue = OrmLiteSettings.ActorSetting;

    private ActorSettingScope() => OrmLiteSettings.ResetForTests();

    public static ActorSettingScope Fresh() => new();

    public void Dispose()
    {
        OrmLiteSettings.ResetForTests();
        OrmLiteSettings.UseActorSetting(_hostValue);
    }
}
