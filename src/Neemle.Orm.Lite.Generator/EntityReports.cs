using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.Orm.Lite.OrmGenerator.Entity;

namespace Neemle.Orm.Lite;

/// <summary>
/// Reports every diagnostic about the entities of the compilation, in a fixed order: the type issues of all the
/// entities first, then, for the entities that are generated, each kind of declaration over all of them.
/// </summary>
internal static class EntityReports
{
    internal static void Report(SourceProductionContext spc, ImmutableArray<Entity> all, ProviderSet providers)
    {
        var entities = all.Where(e => !OrmGenerator.Dropped(e)).ToImmutableArray();
        ShapeDiagnostics.ReportTypeIssues(spc, all);
        foreach (var e in entities) ShapeDiagnostics.ReportUnmapped(spc, e);
        foreach (var e in entities) SoftDeleteDiagnostics.ReportColumnIssue(spc, e);
        foreach (var e in entities) IndexDiagnostics.ReportIndexDeclarations(spc, e);
        foreach (var e in entities) SoftDeleteDiagnostics.ReportSoftDeclaration(spc, e);
        foreach (var e in entities) ShapeDiagnostics.ReportCompositeKey(spc, e);
        TableNameAudit.Report(spc, entities);
        ShapeDiagnostics.ReportMemberIssues(spc, entities);
        foreach (var e in entities)
        {
            CapabilityDiagnostics.ReportCapabilities(spc, e, providers);
            IdentifierDiagnostics.ReportIdentifierLengths(spc, e, providers);
        }
    }
}
