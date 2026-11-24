using System.Runtime.CompilerServices;
using Neemle.OrmLite.Postgres;

namespace Neemle.OrmLite.Tests;

internal static class TestBootstrap
{
    [ModuleInitializer]
    internal static void RegisterProviders() => ProviderRegistry.Register(PostgresProvider.Instance);
}
