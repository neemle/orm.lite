using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.Orm.Lite.OrmGenerator.Entity;
using IndexDef = Neemle.Orm.Lite.OrmGenerator.IndexDef;

namespace Neemle.Orm.Lite;

/// <summary>
/// The diagnostics about the capabilities that an entity declaration needs and a provider of the project may lack: the
/// capabilities each declaration needs, and the providers named in the project that have no capability manifest.
/// </summary>
internal static class CapabilityDiagnostics
{
    internal const string PartialIndexCapability = "PartialIndex";

    internal const string CatalogGuardCapability = "CatalogGuard";

    internal const string IndexNullsCapability = "IndexNullsOrdering";

    internal const string IndexCollationCapability = "IndexCollation";

    internal const string IndexExpressionCapability = "IndexExpression";

    internal const string JsonDocumentCapability = "JsonDocument";

    internal const string RowHistoryTriggersCapability = "RowHistoryTriggers";

    internal const string ObjectMarkerCapability = "ObjectMarker";

    internal const string ExactDecimalCapability = "ExactDecimal";

    private static readonly DiagnosticDescriptor CapabilityNotSupported = new DiagnosticDescriptor(
        id: "ORMLITE015",
        title: "An entity declaration needs a capability that a provider of the project does not support",
        messageFormat:
            "'{0}' declares {1}, which needs the {2} capability; provider '{3}' does not support it. "
            + "Remove the declaration or take '{3}' out of OrmLiteProviders.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ProviderWithoutManifest = new DiagnosticDescriptor(
        id: "ORMLITE019",
        title: "A provider named in OrmLiteProviders has no capability manifest",
        messageFormat:
            "Provider '{0}' is named in OrmLiteProviders but no ormlite.capabilities.{0}.txt file was supplied as "
            + "an additional file, so no declaration is checked against it",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    internal static void ReportProviders(SourceProductionContext spc, ProviderSet set)
    {
        foreach (var provider in set.Providers.Where(p => set.Find(p) is null))
            spc.ReportDiagnostic(Diagnostic.Create(ProviderWithoutManifest, Location.None, provider));
    }

    internal static void ReportCapabilities(SourceProductionContext spc, Entity e, ProviderSet providers)
    {
        if (providers.Providers.IsEmpty) return;
        var needs = NeedsOf(e);
        foreach (var provider in providers.Providers)
        {
            if (providers.Find(provider) is not { } manifest) continue;
            foreach (var (capability, what, location) in needs)
                if (CapabilityManifest.Refuses(manifest, capability))
                    spc.ReportDiagnostic(Diagnostic.Create(
                        CapabilityNotSupported, ShapeDiagnostics.Placed(location, e), e.Type, what, capability,
                        provider));
        }
    }

    private static IEnumerable<(string Capability, string What, Location Location)> NeedsOf(Entity e) =>
        ObjectNeeds(e).Concat(IndexNeeds(e)).Concat(ColumnNeeds(e)).Concat(SoftDeleteNeeds(e));

    private static IEnumerable<(string Capability, string What, Location Location)> ObjectNeeds(Entity e)
    {
        if (e.IsView)
            yield return (
                ObjectMarkerCapability, "the [View] (its definition hash is recorded as a marker)", Location.None);
        if (e.Managed && e.Indexes.Length > 0)
            yield return (
                CatalogGuardCapability, "indexes (the generated reconcile checks the catalog)", Location.None);
    }

    private static IEnumerable<(string Capability, string What, Location Location)> IndexNeeds(Entity e)
    {
        if (!e.Managed) yield break;
        foreach (var ix in e.Indexes.Where(i => i.LiveOnly && e.SoftDeleteColumn is not null))
            yield return (PartialIndexCapability, "the LiveOnly index '" + ix.Name + "'", ix.Location ?? Location.None);
        foreach (var ix in e.Indexes)
            foreach (var need in IndexFeatures(ix))
                yield return need;
    }

    private static IEnumerable<(string Capability, string What, Location Location)> IndexFeatures(IndexDef ix)
    {
        var at = ix.Location ?? Location.None;
        var named = "the index '" + ix.Name + "' with ";
        if (ix.Terms.Any(t => t.Nulls != 0))
            yield return (IndexNullsCapability, named + "a NULLS FIRST or NULLS LAST term", at);
        if (ix.Terms.Any(t => t.Collation is not null))
            yield return (IndexCollationCapability, named + "a COLLATE term", at);
        if (ix.Terms.Any(t => t.Lower)) yield return (IndexExpressionCapability, named + "a LOWER term", at);
        if (!ix.Filter.IsEmpty) yield return (PartialIndexCapability, named + "a Where predicate", at);
    }

    private static IEnumerable<(string Capability, string What, Location Location)> ColumnNeeds(Entity e)
    {
        foreach (var p in e.Props.Where(p => p.Read == "Jsonb"))
            yield return (JsonDocumentCapability, "the Jsonb property '" + p.Name + "'", Location.None);
        foreach (var p in e.Props.Where(p => p.Read == "Decimal"))
            yield return (ExactDecimalCapability, "the decimal property '" + p.Name + "'", Location.None);
    }

    private static IEnumerable<(string Capability, string What, Location Location)> SoftDeleteNeeds(Entity e)
    {
        if (e.SoftDeleteColumn is null || e.Soft is not { } soft) yield break;
        yield return (RowHistoryTriggersCapability, "[SoftDelete] (the row history)", soft.Location);
        yield return (ObjectMarkerCapability, "[SoftDelete] (the history marks the triggers)", soft.Location);
    }
}
