using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.OrmLite.OrmGenerator.Entity;
using Prop = Neemle.OrmLite.OrmGenerator.Prop;

namespace Neemle.OrmLite;

/// <summary>
/// The diagnostics about the shape of an entity type: a property with no column type, a member the generated code
/// cannot handle, a type it cannot name or build, and an entity with more than one key.
/// </summary>
internal static class ShapeDiagnostics
{
    private static readonly DiagnosticDescriptor UnmappedPropertyType = new DiagnosticDescriptor(
        id: "ORMLITE001",
        title: "Orm.Lite has no column mapping for this property's type",
        messageFormat:
            "Orm.Lite has no column mapping for property '{0}.{1}' of type '{2}'.{3} "
            + "Use a supported type (string, int, long, bool, double, decimal, Guid, DateTime, DateOnly or an enum, "
            + "each also nullable), type the property as Neemle.OrmLite.Jsonb to store it as a jsonb column, or "
            + "mark it [NotAColumn] to leave it out. The generator no longer falls back to a text column, so this "
            + "property is not emitted.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor CompositeKey = new DiagnosticDescriptor(
        id: "ORMLITE022",
        title: "An entity must not have more than one key property",
        messageFormat:
            "'{0}' declares {1} key properties ({2}). Orm.Lite supports one key per entity: a get, update or "
            + "delete by key on several keys would act on every row that shares the first, so none is "
            + "generated for '{0}'. Keep one [Key] (a property named Id is a key even without [Key]: remove "
            + "the other [Key], or rename the Id property and keep its column with [Column]), declare no key "
            + "to insert and query only, or address rows by their columns through Query().Where(...).",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnassignableMember = new DiagnosticDescriptor(
        id: "ORMLITE023",
        title: "A property must be readable and assignable by the generated code",
        messageFormat: "Property '{0}.{1}' cannot be handled by the generated code: {2}",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnbuildableEntity = new DiagnosticDescriptor(
        id: "ORMLITE024",
        title: "An entity type must be constructible by the generated code",
        messageFormat: "Entity '{0}' cannot be built by the generated code: {1}",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor UnnameableEntity = new DiagnosticDescriptor(
        id: "ORMLITE025",
        title: "An entity type must be nameable by the generated code",
        messageFormat: "Entity '{0}' cannot be named by the generated code: {1}",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>A location that is none falls back to the location of the entity.</summary>
    internal static Location Placed(Location location, Entity e) =>
        location.Kind == LocationKind.None ? e.Location ?? Location.None : location;

    internal static void ReportUnmapped(SourceProductionContext spc, Entity e)
    {
        foreach (var u in e.Unmapped)
            spc.ReportDiagnostic(Diagnostic.Create(
                UnmappedPropertyType, Placed(u.Location, e), e.Type, u.Property, u.TypeName, u.Note));
    }

    internal static void ReportTypeIssues(SourceProductionContext spc, ImmutableArray<Entity> all)
    {
        foreach (var e in all)
        {
            var location = e.Location ?? Location.None;
            if (e.NameIssue is not null)
                spc.ReportDiagnostic(
                    Diagnostic.Create(UnnameableEntity, location, e.Type, EntityNaming.NameText(e.NameIssue)));
            else if (e.TypeIssue is not null)
                spc.ReportDiagnostic(
                    Diagnostic.Create(UnbuildableEntity, location, e.Type, Buildability.TypeText(e.TypeIssue)));
        }
    }

    internal static void ReportMemberIssues(SourceProductionContext spc, ImmutableArray<Entity> entities)
    {
        foreach (var e in entities)
        {
            if (e.MemberIssues.IsDefaultOrEmpty) continue;
            foreach (var issue in e.MemberIssues)
            {
                var at = Placed(issue.Location, e);
                spc.ReportDiagnostic(Diagnostic.Create(UnassignableMember, at, e.Type, issue.Member, issue.Reason));
            }
        }
    }

    internal static void ReportCompositeKey(SourceProductionContext spc, Entity e)
    {
        if (!OrmGenerator.Refused(e)) return;
        var keys = e.Props.Where(p => p.IsKey).Select(KeyLabel).ToList();
        var location = e.Location ?? Location.None;
        spc.ReportDiagnostic(Diagnostic.Create(CompositeKey, location, e.Type, keys.Count, string.Join(", ", keys)));
    }

    private static string KeyLabel(Prop p) => p.KeyByName ? p.Name + " (by name)" : p.Name;
}
