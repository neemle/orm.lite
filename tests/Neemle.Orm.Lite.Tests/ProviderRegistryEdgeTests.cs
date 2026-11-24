using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests;

public sealed class ProviderRegistryEdgeTests
{
    private const string StubId = "aa_edge_stub";

    private sealed class StubProvider : ISqlProvider
    {
        public string Id => StubId;

        public CapabilitySet Capabilities { get; } = CapabilitySet.Everything();

        public ISerializer CreateSerializer() => SqliteProvider.Instance.CreateSerializer();
    }

    private static StubProvider Registered()
    {
        var stub = new StubProvider();
        ProviderRegistry.Register(stub);
        return stub;
    }

    [Fact(DisplayName = "Find trims the id and matches it without regard to case, as Resolve does")]
    public void Find_is_as_forgiving_as_resolve()
    {
        var stub = Registered();
        // The registry is process-wide static state and this assembly registers only PostgreSQL at load; SQLite
        // is registered by whichever test class constructs first, so register it here and do not rely on that.
        SqliteProviderFactory.Register();

        Assert.Same(stub, ProviderRegistry.Find("  " + StubId.ToUpperInvariant() + " "));
        Assert.Same(stub, ProviderRegistry.Require(" " + StubId));
        Assert.Contains(SqliteProviderFactory.Instance.Id, ProviderRegistry.Installed);
        Assert.Equal(SqliteProvider.Instance.Id, ProviderRegistry.Find(" SQLite ")!.Id);
        Assert.Null(ProviderRegistry.Find("   "));
    }

    [Theory(DisplayName = "Require refuses a blank id as an argument error, not as an IndexOutOfRangeException")]
    [InlineData("")]
    [InlineData("   ")]
    public void Require_refuses_a_blank_id(string id)
    {
        Assert.Throws<ArgumentException>(() => ProviderRegistry.Require(id));
        Assert.ThrowsAny<ArgumentException>(() => ProviderRegistry.Require(null!));
    }

    [Fact(DisplayName = "a provider registered only as an ISqlProvider is listed, found, and explained by Resolve "
        + "as having no factory instead of as not installed")]
    public void A_provider_without_a_factory_is_explained()
    {
        Registered();

        var refusal = Assert.Throws<InvalidOperationException>(
            () => ProviderRegistry.Resolve(new OrmLiteOptions { Provider = StubId }));

        Assert.Contains(StubId, ProviderRegistry.Installed);
        Assert.Contains("registered as an ISqlProvider only", refusal.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("is not installed", refusal.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "an id that is registered nowhere still reads 'is not installed' with the package to add")]
    public void An_unknown_id_is_still_not_installed()
    {
        var refusal = Assert.Throws<InvalidOperationException>(() => ProviderRegistry.Require("oracle"));

        Assert.Contains(
            "provider 'oracle' is not installed; add Neemle.Orm.Lite.Oracle",
            refusal.Message,
            StringComparison.Ordinal);
    }
}
