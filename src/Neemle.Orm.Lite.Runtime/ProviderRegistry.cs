using System.Collections.Concurrent;
using System.Data.Common;

namespace Neemle.Orm.Lite;

public interface IProviderFactory
{
    string Id { get; }

    ISqlProvider CreateProvider(OrmLiteOptions options);

    DbDataSource CreateDataSource(OrmLiteOptions options, DbLaneKind lane);

    Task InitializeAsync(OrmLiteOptions options, DbDataSource source, CancellationToken ct) => Task.CompletedTask;
}

public static class ProviderRegistry
{
    private static readonly ConcurrentDictionary<string, ISqlProvider> Providers =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<string, IProviderFactory> Factories = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Installed =>
        Providers.Keys.Concat(Factories.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();

    public static void Register(ISqlProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        Providers[provider.Id] = provider;
    }

    public static void Register(IProviderFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        var seen = Factories.GetOrAdd(factory.Id, factory);
        if (!ReferenceEquals(seen, factory) && seen.GetType() != factory.GetType())
            throw new InvalidOperationException(
                $"provider '{factory.Id}' is already registered by {seen.GetType().FullName}; a second provider cannot take the same id.");
    }

    public static ISqlProvider? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        id = id.Trim();
        if (Providers.TryGetValue(id, out var provider)) return provider;
        return Factories.TryGetValue(id, out var factory) ? factory.CreateProvider(new OrmLiteOptions()) : null;
    }

    public static ISqlProvider Require(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return Find(id) ?? throw new InvalidOperationException(
            $"provider '{id}' is not installed; add Neemle.Orm.Lite.{Capitalized(id.Trim())} "
            + "and register its provider.");
    }

    public static IProviderFactory Resolve(OrmLiteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var listing = Installed.Count == 0 ? "none" : string.Join(", ", Installed);
        if (string.IsNullOrWhiteSpace(options.Provider))
            throw new InvalidOperationException(
                "no provider is configured: set OrmLite:Provider to one of the installed providers. "
                + $"Installed providers: {listing}. There is no default provider.");
        if (Factories.TryGetValue(options.Provider.Trim(), out var factory)) return factory;
        if (Providers.ContainsKey(options.Provider.Trim()))
            throw new InvalidOperationException(
                $"provider '{options.Provider}' is registered as an ISqlProvider only, which has no factory to open "
                + "connections; register an IProviderFactory for it (referencing its package does this). "
                + $"Installed providers: {listing}.");
        throw new InvalidOperationException(
            $"provider '{options.Provider}' is not installed; add the package Neemle.Orm.Lite.{Capitalized(options.Provider.Trim())} "
            + $"to the application project (referencing the package registers the provider). Installed providers: {listing}.");
    }

    private static string Capitalized(string id) => char.ToUpperInvariant(id[0]) + id[1..].ToLowerInvariant();
}
