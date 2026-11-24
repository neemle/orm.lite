using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.OrmLite.OrmGenerator.Entity;

namespace Neemle.OrmLite;

/// <summary>
/// Two [Table] entities that map one table name and are both Managed would both get a CREATE TABLE in the generated
/// baseline. An entity with Managed = false or a [View] does not create the table and may share the name.
/// </summary>
internal static class TableNameAudit
{
    private static readonly DiagnosticDescriptor DuplicateTable = new DiagnosticDescriptor(
        id: "ORMLITE027",
        title: "Two managed entities must not map the same table",
        messageFormat:
            "Table '{1}' is mapped with Managed = true by {2}, so the generated baseline would create it more than "
            + "once. Keep one of them Managed and declare the others with Managed = false, or give each its own "
            + "table name. Reported for '{0}'.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static void Report(SourceProductionContext spc, Entity e, string table, string names) =>
        spc.ReportDiagnostic(Diagnostic.Create(DuplicateTable, e.Location ?? Location.None, e.Type, table, names));

    internal static void Report(SourceProductionContext spc, ImmutableArray<Entity> entities)
    {
        var managed = entities.Where(e => e.Managed && !e.IsView && !OrmGenerator.Dropped(e));
        foreach (var group in managed.GroupBy(e => e.Table, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            var quoted = group.Select(e => "'" + NavigationFacts.Short(e.Full) + "'");
            var names = string.Join(", ", quoted.Order(StringComparer.Ordinal));
            foreach (var e in group) Report(spc, e, group.Key, names);
        }
    }
}
