using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Mig = Neemle.OrmLite.Generator.MigrationGenerator.Mig;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// Checks the versions of the [Migration] classes of one compilation. A version names a migration in
/// schema_migrations (its primary key) and orders the migrations, so an empty or a repeated one cannot work: the
/// runner would apply only one of two migrations with the same version, or fail part way. A migration that fails the
/// check is reported and not emitted.
/// </summary>
internal static class MigrationVersions
{
    private static readonly DiagnosticDescriptor DuplicateVersion = new DiagnosticDescriptor(
        id: "ORMLITE028",
        title: "Two [Migration] classes must not share a version",
        messageFormat:
            "[Migration(\"{1}\")] on '{0}' has the same version as {2}. The version is the primary key of "
            + "schema_migrations, so only one of them could ever run. Give each migration its own version; none of "
            + "them is emitted.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EmptyVersion = new DiagnosticDescriptor(
        id: "ORMLITE029",
        title: "A [Migration] must declare a version",
        messageFormat:
            "[Migration] on '{0}' has an empty version. The version names the migration in schema_migrations and "
            + "orders it among the others; give it a text such as \"0100_add_notes\". The migration is not emitted.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Reports every empty or repeated version and returns the migrations that passed.</summary>
    internal static ImmutableArray<Mig> Check(SourceProductionContext spc, ImmutableArray<Mig> migs)
    {
        var accepted = ImmutableArray.CreateBuilder<Mig>();
        foreach (var group in migs.GroupBy(m => m.Version, StringComparer.Ordinal))
        {
            var members = group.OrderBy(m => m.Type, StringComparer.Ordinal).ToList();
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                foreach (var m in members) Report(spc, EmptyVersion, m);
            }
            else if (members.Count > 1)
            {
                foreach (var m in members) Report(spc, DuplicateVersion, m, Others(members, m));
            }
            else
            {
                accepted.Add(members[0]);
            }
        }
        return accepted.ToImmutable();
    }

    private static void Report(SourceProductionContext spc, DiagnosticDescriptor rule, Mig m, string? others = null) =>
        spc.ReportDiagnostic(Diagnostic.Create(rule, m.Location, m.Type, m.Version, others));

    private static string Others(List<Mig> members, Mig self) =>
        string.Join(", ", members.Where(m => !ReferenceEquals(m, self)).Select(m => "'" + m.Type + "'"));
}
