using System.Runtime.CompilerServices;
using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests;

internal static class TestBootstrap
{
    [ModuleInitializer]
    internal static void RegisterProviders() => ProviderRegistry.Register(PostgresProvider.Instance);
}
