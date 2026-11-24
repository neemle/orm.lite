using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Collection = Neemle.OrmLite.OrmGenerator.Collection;
using Entity = Neemle.OrmLite.OrmGenerator.Entity;
using Prop = Neemle.OrmLite.OrmGenerator.Prop;
using Relation = Neemle.OrmLite.OrmGenerator.Relation;
using static Neemle.OrmLite.SymbolFacts;

namespace Neemle.OrmLite;

/// <summary>
/// Reads one table or view type into an entity model. The reading has three passes: the EntityScan sorts each
/// property into its list, the relations are resolved against the columns that were found, and the entity is
/// assembled with the indexes, the soft-delete declaration and the findings. Nothing here keeps a symbol or a
/// compilation in the model, so the incremental pipeline can compare models by value.
/// </summary>
internal static class EntityReader
{
    internal static Entity ReadEntity(INamedTypeSymbol s, bool view)
    {
        var bound = s.IsStatic ? null : Buildability.BoundConstructor(s);
        var scan = new EntityScan(s, view, Buildability.SetsRequiredMembers(bound));
        foreach (var m in PropertiesOf(s)) scan.Visit(m);
        return Assemble(scan, TableOf(s, view), bound);
    }

    private static string TableOf(INamedTypeSymbol s, bool view) =>
        AttrArg(s, view ? "Neemle.OrmLite.ViewAttribute" : "Neemle.OrmLite.TableAttribute") ?? Snake(s.Name);

    private static Entity Assemble(EntityScan scan, string table, IMethodSymbol? bound)
    {
        var s = scan.Type;
        var props = scan.Props;
        var found = scan.Found;
        var indexes = IndexReader.ReadIndexes(s, table, props, scan.OnProperties);
        var relations = ResolveRelations(scan.Navs, props, found);
        var typeIssue = Buildability.TypeIssueOf(s, bound);
        var requiredMissing = Buildability.AddRequiredIssues(
            s, props, scan.UnmappedProps, scan.SetsRequired, found.Issues);
        var keyProp = props.FirstOrDefault(p => p.IsKey)?.Name ?? "Id";
        var collections = scan.Collections.Select(c => CollectionOf(c, keyProp)).ToImmutableArray();
        var managed = !scan.View && NamedBoolOrDefault(s, "Neemle.OrmLite.TableAttribute", "Managed", true);
        var (softDeleteColumn, softDeleteIssue) = SoftDeleteReader.ReadSoftDelete(s, props);
        return new Entity(
            s.Name, table,
            props.ToImmutableArray(), indexes, relations.ToImmutableArray(), collections, scan.Links.ToImmutableArray(),
            managed, scan.UnmappedProps.ToImmutableArray(), softDeleteColumn, softDeleteIssue,
            SoftDeleteReader.ReadSoftDeclaration(s, props), EntityNaming.AccessOf(s), scan.View, LocationOf(s),
            found.Issues.ToImmutableArray(), typeIssue, requiredMissing, found.LostKeys.ToImmutableArray(),
            found.Excluded.ToImmutableArray(), s.IsStatic)
        { Full = FullNameOf(s), NameIssue = EntityNaming.NameIssueOf(s) };
    }

    /// <summary>Pairs each navigation with its foreign key column; one without a usable key leaves a finding.</summary>
    private static List<Relation> ResolveRelations(
        IEnumerable<EntityScan.NavRef> navs, List<Prop> props, Findings found)
    {
        var relations = new List<Relation>();
        foreach (var n in navs)
        {
            var fk = props.FirstOrDefault(p => p.Name == n.Fk);
            var relation = new Relation(n.Nav, n.Target.Name, n.Fk, fk?.Nullable ?? false)
            {
                TargetFull = FullNameOf(n.Target),
                FkCs = fk?.Cs,
                FkValue = fk?.NullableValue ?? false,
            };
            if (fk is null || n.Issue is null) relations.Add(relation);
            else found.Add(n.Nav, n.Issue, n.Location);
        }
        return relations;
    }

    private static Collection CollectionOf((string Nav, INamedTypeSymbol Child, string InvFk) c, string parentKey) =>
        new Collection(c.Nav, c.Child.Name, c.InvFk, parentKey) { ChildFull = FullNameOf(c.Child) };
}
