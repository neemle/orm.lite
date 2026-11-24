using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.OrmLite.OrmGenerator.Entity;
using IndexDef = Neemle.OrmLite.OrmGenerator.IndexDef;

namespace Neemle.OrmLite;

/// <summary>The diagnostics about the [Index] declarations of an entity.</summary>
internal static class IndexDiagnostics
{
    private static readonly DiagnosticDescriptor InvalidIndexDeclaration = new DiagnosticDescriptor(
        id: "ORMLITE020",
        title: "An [Index] declaration is not valid",
        messageFormat: "Index '{1}' on '{0}': {2}",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ConflictingIndexName = new DiagnosticDescriptor(
        id: "ORMLITE021",
        title: "Two indexes of one entity share a name but declare different definitions",
        messageFormat:
            "Index name '{1}' on '{0}' is used by two indexes that differ in columns, order, collation, uniqueness "
            + "or predicate; only one would be created. Give each its own Name.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidLiveOnlyIndex = new DiagnosticDescriptor(
        id: "ORMLITE004",
        title: "A LiveOnly index must be a Unique index on a [SoftDelete] entity",
        messageFormat:
            "Index '{1}' on '{0}' is declared LiveOnly but {2}. A LiveOnly index is a partial unique index over "
            + "the live rows of a [SoftDelete] entity.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateIndexName = new DiagnosticDescriptor(
        id: "ORMLITE005",
        title: "An explicit index Name must not repeat another index of the same entity",
        messageFormat:
            "Index name '{1}' on '{0}' is declared explicitly and is already the name of another index on the "
            + "same table; the second index of that name would silently not be created. Give each index its own "
            + "name.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static void ReportIndexDeclarations(SourceProductionContext spc, Entity e)
    {
        ReportIndexProblems(spc, e);
        foreach (var ix in e.Indexes)
        {
            var location = ix.Location ?? Location.None;
            var reasons = LiveOnlyReasons(e, ix);
            if (reasons.Count > 0)
                spc.ReportDiagnostic(
                    Diagnostic.Create(InvalidLiveOnlyIndex, location, e.Type, ix.Name, string.Join(" and ", reasons)));
            if (ix.ExplicitName && e.Indexes.Count(o => o.Name == ix.Name) > 1)
                spc.ReportDiagnostic(Diagnostic.Create(DuplicateIndexName, location, e.Type, ix.Name));
        }
    }

    private static List<string> LiveOnlyReasons(Entity e, IndexDef ix)
    {
        var reasons = new List<string>();
        if (ix.LiveOnly && !DeclaresSoftDelete(e)) reasons.Add("the entity is not [SoftDelete]");
        if (ix.LiveOnly && !ix.Unique) reasons.Add("the index is not Unique");
        return reasons;
    }

    private static bool DeclaresSoftDelete(Entity e) => e.SoftDeleteColumn is not null || e.SoftDeleteIssue is not null;

    private static void ReportIndexProblems(SourceProductionContext spc, Entity e)
    {
        foreach (var ix in e.Indexes)
        {
            var location = ix.Location ?? Location.None;
            foreach (var problem in ix.Problems)
                spc.ReportDiagnostic(Diagnostic.Create(InvalidIndexDeclaration, location, e.Type, ix.Name, problem));
            if (Conflicts(e, ix) && e.Indexes.First(o => o.Name == ix.Name) == ix)
                spc.ReportDiagnostic(Diagnostic.Create(ConflictingIndexName, location, e.Type, ix.Name));
        }
    }

    private static bool Conflicts(Entity e, IndexDef ix) =>
        e.Indexes.Any(o => !ReferenceEquals(o, ix) && o.Name == ix.Name
            && !(o.ExplicitName && ix.ExplicitName) && !SameDefinition(o, ix));

    private static bool SameDefinition(IndexDef a, IndexDef b) =>
        a.Unique == b.Unique && a.LiveOnly == b.LiveOnly && a.Terms.SequenceEqual(b.Terms)
        && a.Filter.SequenceEqual(b.Filter);
}
