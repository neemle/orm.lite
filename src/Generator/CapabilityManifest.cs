using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Neemle.OrmLite;

internal sealed record ProviderManifest(string Provider, ImmutableDictionary<string, string> Levels)
{
    internal string LevelOf(string capability) =>
        Levels.TryGetValue(capability, out var level) ? level : CapabilityManifest.None;

    public bool Equals(ProviderManifest? other) =>
        other is not null && Provider == other.Provider && Levels.Count == other.Levels.Count
        && Levels.All(pair => other.Levels.TryGetValue(pair.Key, out var level) && level == pair.Value);

    public override int GetHashCode() => (Provider, Levels.Count).GetHashCode();

    // The most bytes of an identifier the provider keeps; null when the manifest states no limit or a bad one.
    internal int? MaxIdentifierBytes =>
        Levels.TryGetValue(CapabilityManifest.MaxIdentifierBytesKey, out var text)
        && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var limit)
        && limit > 0
            ? limit
            : null;
}

internal sealed record ProviderSet(EquatableArray<string> Providers, EquatableArray<ProviderManifest> Manifests)
{
    internal static readonly ProviderSet Empty = new([], []);

    internal ProviderManifest? Find(string provider) =>
        Manifests.FirstOrDefault(m => string.Equals(m.Provider, provider, StringComparison.Ordinal));
}

internal static class CapabilityManifest
{
    internal const string ProvidersProperty = "build_property.OrmLiteProviders";

    internal const string FilePrefix = "ormlite.capabilities.";

    internal const string FileSuffix = ".txt";

    internal const string None = "None";

    internal const string MaxIdentifierBytesKey = "MaxIdentifierBytes";

    internal static ImmutableArray<string> ProviderIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        return [.. raw!.Split([';', ',', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)];
    }

    internal static ProviderManifest? Parse(AdditionalText file, System.Threading.CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(file.Path);
        if (!name.StartsWith(FilePrefix, StringComparison.OrdinalIgnoreCase)
            || !name.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase))
            return null;
        var provider = name
            .Substring(FilePrefix.Length, name.Length - FilePrefix.Length - FileSuffix.Length)
            .ToLowerInvariant();
        var levels = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var line in (file.GetText(cancellationToken)?.ToString() ?? "").Split(['\n']))
        {
            var split = line.Trim().Split(['=']);
            if (split.Length == 2 && split[0].Trim().Length > 0) levels[split[0].Trim()] = split[1].Trim();
        }
        return new ProviderManifest(provider, levels.ToImmutable());
    }

    internal static ProviderSet Read(
        AnalyzerConfigOptionsProvider options, ImmutableArray<AdditionalText> files,
        System.Threading.CancellationToken cancellationToken)
    {
        options.GlobalOptions.TryGetValue(ProvidersProperty, out var raw);
        var manifests = files
            .Select(f => Parse(f, cancellationToken)).Where(m => m is not null).Select(m => m!).ToImmutableArray();
        return new ProviderSet(ProviderIds(raw), manifests);
    }

    internal static bool Refuses(ProviderManifest manifest, string capability) =>
        string.Equals(manifest.LevelOf(capability), None, StringComparison.Ordinal);
}
