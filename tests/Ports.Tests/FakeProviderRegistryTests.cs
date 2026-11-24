using Neemle.OrmLite.Ports.Tests.Fake;
using Neemle.OrmLite.Sql;
using Xunit;

namespace Neemle.OrmLite.Ports.Tests;

public sealed class FakeProviderRegistryTests
{
    private static readonly string[] ShippedProviderAssemblies =
        ["Neemle.OrmLite.Postgres", "Neemle.OrmLite.Sqlite", "Npgsql", "Microsoft.Data.Sqlite"];

    [Fact(DisplayName = "a third provider registered by its id is found by that id and listed among the "
        + "installed ones")]
    public void A_registered_third_provider_is_found_by_its_id()
    {
        var provider = new FakeProvider(new FakeDatabase());

        ProviderRegistry.Register(provider);

        Assert.Same(provider, ProviderRegistry.Require(FakeProvider.ProviderId));
        Assert.Contains(FakeProvider.ProviderId, ProviderRegistry.Installed);
    }

    [Fact(DisplayName = "an id nobody registered is refused by name; the registry has no default provider to "
        + "fall back on")]
    public void An_unregistered_id_is_refused()
    {
        const string id = "nobody-registered-this";

        Assert.Null(ProviderRegistry.Find(id));
        var refusal = Assert.Throws<InvalidOperationException>(() => ProviderRegistry.Require(id));
        Assert.Contains(id, refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "this test process never loaded a shipped provider or a driver: the core ran with the "
        + "ports alone")]
    public void No_shipped_provider_or_driver_is_loaded()
    {
        var core = new[] { typeof(ProviderRegistry).Assembly, typeof(StandardSerializer).Assembly };

        var loaded = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name).ToList();

        Assert.Equal(new[] { "Neemle.OrmLite.Runtime", "Neemle.OrmLite.Sql" }, core.Select(a => a.GetName().Name));
        Assert.DoesNotContain(loaded, name => ShippedProviderAssemblies.Contains(name, StringComparer.Ordinal));
    }
}
