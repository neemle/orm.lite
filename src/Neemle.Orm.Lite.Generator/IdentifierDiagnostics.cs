using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Entity = Neemle.Orm.Lite.OrmGenerator.Entity;

namespace Neemle.Orm.Lite;

/// <summary>The diagnostic about a name of an entity that is longer than the identifier limit of a provider.</summary>
internal static class IdentifierDiagnostics
{
    private static readonly DiagnosticDescriptor IdentifierTooLong = new DiagnosticDescriptor(
        id: "ORMLITE034",
        title: "A name of an entity is longer than the identifier limit of a provider of the project",
        messageFormat: "'{0}': the {1} name '{2}' is {3} bytes in UTF-8 and provider '{4}' keeps at most {5}; "
            + "it would be cut there and could end up the same as another name. Give it a shorter name.",
        category: "OrmLite",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static void ReportIdentifierLengths(SourceProductionContext spc, Entity e, ProviderSet providers)
    {
        foreach (var provider in providers.Providers)
        {
            if (providers.Find(provider)?.MaxIdentifierBytes is not { } limit) continue;
            foreach (var (kind, name, location) in NamesOf(e))
            {
                var bytes = Encoding.UTF8.GetByteCount(name);
                if (bytes > limit)
                    spc.ReportDiagnostic(Diagnostic.Create(
                        IdentifierTooLong, location, e.Type, kind, name, bytes, provider, limit));
            }
        }
    }

    private static IEnumerable<(string Kind, string Name, Location Location)> NamesOf(Entity e)
    {
        var at = e.Location ?? Location.None;
        yield return (e.IsView ? "view" : "table", e.Table, at);
        foreach (var p in e.Props) yield return ("column", p.Column, at);
        foreach (var ix in e.Indexes) yield return ("index", ix.Name, ix.Location ?? at);
        foreach (var mm in e.ManyToMany) yield return ("join table", mm.JoinTable, at);
    }
}
