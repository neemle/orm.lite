using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;
using SoftDeclaration = Neemle.OrmLite.Generator.OrmGenerator.SoftDeclaration;

namespace Neemle.OrmLite.Generator;

/// <summary>The diagnostics about the [SoftDelete] declaration of an entity and the default tenant property.</summary>
internal static class SoftDeleteDiagnostics
{
    private static readonly DiagnosticDescriptor InvalidSoftDeleteColumn = new DiagnosticDescriptor(
        id: "ORMLITE002",
        title: "A [SoftDelete] column must be a mapped System.DateTime? property",
        messageFormat:
            "[SoftDelete] on '{0}' names '{1}', which {2}. "
            + "The soft-delete column must be a mapped System.DateTime? property of the entity.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftTenant = new DiagnosticDescriptor(
        id: "ORMLITE007",
        title: "A [SoftDelete] entity needs a Guid tenant column or Tenant = Platform",
        messageFormat:
            "[SoftDelete] on '{0}' has no usable tenant: Tenant '{1}' {2}. "
            + "Declare a System.Guid tenant property ({3} by default, or name it with Tenant = ...), "
            + "or declare Tenant = SoftDeleteAttribute.Platform for an entity with no tenant.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidDefaultTenant = new DiagnosticDescriptor(
        id: "ORMLITE014",
        title: "OrmLiteDefaultTenant must be a C# identifier",
        messageFormat:
            "The OrmLiteDefaultTenant build property is '{0}', which is not a C# identifier. "
            + "Set it to the name of the System.Guid property that a [SoftDelete] entity carries as its tenant by "
            + "default, for example TenantId.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftKey = new DiagnosticDescriptor(
        id: "ORMLITE008",
        title: "A [SoftDelete] entity needs exactly one System.Guid key",
        messageFormat:
            "[SoftDelete] on '{0}' requires exactly one non-nullable System.Guid key property, but {1}",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftUnversioned = new DiagnosticDescriptor(
        id: "ORMLITE009",
        title: "A [SoftDelete] Unversioned name must be a mapped column",
        messageFormat:
            "[SoftDelete] on '{0}' lists '{1}' in Unversioned, which is not a mapped property of the entity. "
            + "A misspelled name would leave the column in the history rows.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftReference = new DiagnosticDescriptor(
        id: "ORMLITE010",
        title: "A [SoftDelete] Label or Owners name must be a mapped column",
        messageFormat: "[SoftDelete] on '{0}' names '{2}' in {1}, which is not a mapped property of the entity",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftLabelType = new DiagnosticDescriptor(
        id: "ORMLITE011",
        title: "A [SoftDelete] Label must be a text column",
        messageFormat:
            "[SoftDelete] on '{0}' names '{1}' as its Label, which is of type '{2}', not string. "
            + "A label is a text column.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidSoftColumnList = new DiagnosticDescriptor(
        id: "ORMLITE012",
        title: "A [SoftDelete] Pinned or StatusColumns name must be a mapped column",
        messageFormat: "[SoftDelete] on '{0}' names '{2}' in {1}, which is not a mapped property of the entity",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor StatusColumnIsPinned = new DiagnosticDescriptor(
        id: "ORMLITE013",
        title: "A [SoftDelete] StatusColumns name must not also be Pinned",
        messageFormat:
            "[SoftDelete] on '{0}' lists '{1}' in both Pinned and StatusColumns. "
            + "A column the restore keeps cannot also be one the human's option governs.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static void ReportDefaultTenant(SourceProductionContext spc, string value)
    {
        if (value.Length > 0 && !DefaultTenants.IsIdentifier(value))
            spc.ReportDiagnostic(Diagnostic.Create(InvalidDefaultTenant, Location.None, value));
    }

    internal static void ReportColumnIssue(SourceProductionContext spc, Entity e)
    {
        if (e.SoftDeleteIssue is { } issue)
            spc.ReportDiagnostic(Diagnostic.Create(
                InvalidSoftDeleteColumn, issue.Location, e.Type, issue.Property, issue.Reason));
    }

    internal static void ReportSoftDeclaration(SourceProductionContext spc, Entity e)
    {
        if (e.SoftDeleteColumn is null || e.Soft is not { } soft) return;
        ReportTenant(spc, e, soft);
        ReportKey(spc, e, soft);
        ReportReferences(spc, e, soft);
        ReportColumnLists(spc, e, soft);
    }

    private static void ReportTenant(SourceProductionContext spc, Entity e, SoftDeclaration soft)
    {
        if (soft.Platform) return;
        var tenantReason = TenantRules.ReasonOf(soft.TenantProp);
        if (tenantReason is not null)
            spc.ReportDiagnostic(Diagnostic.Create(
                InvalidSoftTenant, soft.Location, e.Type, soft.TenantName, tenantReason, soft.DefaultName));
    }

    private static void ReportKey(SourceProductionContext spc, Entity e, SoftDeclaration soft)
    {
        var keyReason = KeyReason(e.Props.Where(p => p.IsKey).ToList());
        if (keyReason is not null)
            spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftKey, soft.Location, e.Type, keyReason));
    }

    private static string? KeyReason(List<Prop> keys)
    {
        if (keys.Count == 0) return "it has none";
        if (keys.Count > 1) return "it has " + keys.Count + " (" + string.Join(", ", keys.Select(k => k.Name)) + ")";
        var key = keys[0];
        return key.Read != "Guid" || key.Nullable ? "'" + key.Name + "' is not a non-nullable System.Guid" : null;
    }

    /// <summary>The Unversioned, Label and Owners names that match no property, and a Label of no text.</summary>
    private static void ReportReferences(SourceProductionContext spc, Entity e, SoftDeclaration soft)
    {
        foreach (var name in soft.UnknownUnversioned)
            spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftUnversioned, soft.Location, e.Type, name));
        if (soft.LabelName is not null && soft.LabelProp is null)
            spc.ReportDiagnostic(
                Diagnostic.Create(InvalidSoftReference, soft.Location, e.Type, "Label", soft.LabelName));
        foreach (var name in soft.UnknownOwners)
            spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftReference, soft.Location, e.Type, "Owners", name));
        if (soft.LabelProp is { Read: not "String" } label)
            spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftLabelType, soft.Location, e.Type, label.Name, label.Cs));
    }

    /// <summary>The Pinned and StatusColumns names that match no mapped property, and the columns in both.</summary>
    private static void ReportColumnLists(SourceProductionContext spc, Entity e, SoftDeclaration soft)
    {
        foreach (var name in soft.UnknownPinned)
            spc.ReportDiagnostic(Diagnostic.Create(InvalidSoftColumnList, soft.Location, e.Type, "Pinned", name));
        foreach (var name in soft.UnknownStatus)
            spc.ReportDiagnostic(
                Diagnostic.Create(InvalidSoftColumnList, soft.Location, e.Type, "StatusColumns", name));
        foreach (var column in soft.StatusPinnedOverlap)
        {
            var name = e.Props.First(p => p.Column == column).Name;
            spc.ReportDiagnostic(Diagnostic.Create(StatusColumnIsPinned, soft.Location, e.Type, name));
        }
    }
}
