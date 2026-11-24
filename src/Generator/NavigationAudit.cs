using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.OrmLite.OrmGenerator.Entity;
using Relation = Neemle.OrmLite.OrmGenerator.Relation;
using Collection = Neemle.OrmLite.OrmGenerator.Collection;

namespace Neemle.OrmLite;

/// <summary>
/// Checks every relation, collection and many-to-many navigation against the entity it points at, because a loader
/// built on a wrong foreign key does not compile (a property that does not exist, a key of another type, a target
/// without a get by key) or fails when it runs (a property that is not a mapped column). A navigation that fails the
/// check gets ORMLITE026 and no loader; the returned set names the withheld loaders as (entity, navigation).
/// </summary>
internal static class NavigationAudit
{
    /// <summary>A problem that another diagnostic already reports: the loader is withheld, no second report.</summary>
    private const string Silent = "";

    private static readonly DiagnosticDescriptor NotLoadable = new DiagnosticDescriptor(
        id: "ORMLITE026",
        title: "A navigation cannot be loaded by the generated code",
        messageFormat: "Navigation '{0}.{1}' cannot be loaded: {2}. No Load{1}Async is generated for it.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static HashSet<(string Entity, string Nav)> Report(
        SourceProductionContext spc, ImmutableArray<Entity> all)
    {
        var blocked = new HashSet<(string Entity, string Nav)>();
        var byFull = new Dictionary<string, Entity>();
        foreach (var e in all) byFull.TryAdd(e.Full, e);
        foreach (var e in all.Where(e => !OrmGenerator.Dropped(e) && !e.IsView))
            foreach (var (nav, reason) in ProblemsOf(e, byFull))
            {
                blocked.Add((e.Full, nav));
                if (reason.Length == 0) continue;
                var at = e.Location ?? Location.None;
                spc.ReportDiagnostic(Diagnostic.Create(NotLoadable, at, e.Type, nav, reason));
            }
        return blocked;
    }

    private static IEnumerable<(string Nav, string Reason)> ProblemsOf(Entity e, Dictionary<string, Entity> byFull)
    {
        foreach (var r in e.Relations)
            if (RelationReason(e, r, byFull) is { } reason) yield return (r.Nav, reason);
        foreach (var c in e.Collections)
            if (CollectionReason(e, c, byFull) is { } reason) yield return (c.Nav, reason);
        foreach (var m in e.ManyToMany)
            if (LinkReason(e) is { } reason) yield return (m.Nav, reason);
    }

    private static string? RelationReason(Entity e, Relation r, Dictionary<string, Entity> byFull)
    {
        if (!byFull.TryGetValue(r.TargetFull, out var target) || OrmGenerator.Dropped(target)) return null;
        if (r.FkCs is null) return Covers(e, r.FkProp) || Covers(e, r.Nav) ? Silent : MissingForeignKey(e, r);
        if (OrmGenerator.KeyedApiWithheld(target)) return null;
        if (target.IsView) return "'" + target.Type + "' is a view, which has no get by key to load it with";
        if (OrmGenerator.FirstKey(target) is not { } key)
            return "'" + target.Type + "' has no key property, so there is no get by key to load it with";
        return NavigationFacts.Passes(r.FkCs, key.Cs)
            ? null
            : "its foreign key '" + r.FkProp + "' is " + NavigationFacts.Short(r.FkCs) + " but the key '" + key.Name
                + "' of '" + target.Type + "' is " + NavigationFacts.Short(key.Cs);
    }

    private static string? CollectionReason(Entity e, Collection c, Dictionary<string, Entity> byFull)
    {
        if (OrmGenerator.KeyedApiWithheld(e)) return null;
        if (OrmGenerator.FirstKey(e) is not { } parentKey)
            return "'" + e.Type + "' has no key property, so its key cannot be matched with the inverse key '"
                + c.InverseFk + "'";
        if (!byFull.TryGetValue(c.ChildFull, out var child) || OrmGenerator.Dropped(child)) return null;
        var inverse = child.Props.FirstOrDefault(p => p.Name == c.InverseFk);
        if (inverse is null) return Covers(child, c.InverseFk) ? null : MissingInverse(child, c);
        return NavigationFacts.Comparable(inverse.Cs, parentKey.Cs)
            ? null
            : "the inverse key '" + c.InverseFk + "' of '" + child.Type + "' is " + NavigationFacts.Short(inverse.Cs)
                + " but the key '" + parentKey.Name + "' of '" + e.Type + "' is " + NavigationFacts.Short(parentKey.Cs);
    }

    private static string? LinkReason(Entity e) =>
        OrmGenerator.KeyedApiWithheld(e) || OrmGenerator.FirstKey(e) is not null
            ? null
            : "'" + e.Type + "' has no key property, so the link table cannot be addressed by its key";

    private static string MissingForeignKey(Entity e, Relation r) =>
        "its foreign key '" + r.FkProp + "' is not a mapped property of '" + e.Type + "'. Add that property, name "
        + "the key property with [Reference(\"...\")] on the navigation, or mark the navigation [NotAColumn]";

    private static string MissingInverse(Entity child, Collection c) =>
        "the inverse key '" + c.InverseFk + "' is not a mapped property of '" + child.Type + "'. Add that property "
        + "to '" + child.Type + "', name the key with [Reference(\"...\")] on the collection, or mark the collection "
        + "[NotAColumn]";

    /// <summary>
    /// True when another diagnostic of the entity already reports the member: it is excluded or has a member issue
    /// (ORMLITE023, for example a required navigation) or is of an unmapped type (ORMLITE001).
    /// </summary>
    private static bool Covers(Entity e, string member) =>
        (!e.ExcludedMembers.IsDefaultOrEmpty && e.ExcludedMembers.Contains(member))
        || e.MemberIssues.Any(i => i.Member == member)
        || e.Unmapped.Any(u => u.Property == member);
}
