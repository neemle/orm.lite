using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using ManyToMany = Neemle.OrmLite.Generator.OrmGenerator.ManyToMany;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;
using Unmapped = Neemle.OrmLite.Generator.OrmGenerator.Unmapped;
using static Neemle.OrmLite.Generator.SymbolFacts;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// The collection pass of the entity reading: it looks at each property of an entity once and sorts it into a column,
/// a navigation to a parent, a collection of children, a many-to-many link, an unmapped property or a member that the
/// generated code cannot handle. The lists it fills are resolved against each other afterwards by the EntityReader.
/// </summary>
internal sealed class EntityScan
{
    private const string TableAttribute = "Neemle.OrmLite.TableAttribute";

    public EntityScan(INamedTypeSymbol type, bool view, bool setsRequired)
    {
        Type = type;
        View = view;
        SetsRequired = setsRequired;
        Found = new Findings(type.ContainingAssembly);
    }

    public INamedTypeSymbol Type { get; }

    public bool View { get; }

    public bool SetsRequired { get; }

    public Findings Found { get; }

    public List<Prop> Props { get; } = new();

    public List<Unmapped> UnmappedProps { get; } = new();

    public List<(Prop Prop, AttributeData Attribute)> OnProperties { get; } = new();

    public List<NavRef> Navs { get; } = new();

    public List<(string Nav, INamedTypeSymbol Child, string InvFk)> Collections { get; } = new();

    public List<ManyToMany> Links { get; } = new();

    /// <summary>A navigation to a parent entity, waiting for its foreign key to be found among the columns.</summary>
    internal sealed record NavRef(string Nav, INamedTypeSymbol Target, string Fk, string? Issue, Location Location);

    /// <summary>Sorts one property into the list that it belongs to, or leaves it out.</summary>
    public void Visit(IPropertySymbol m)
    {
        if (!AccessRules.IsMappable(m)) { Found.Exclude(m, KeyLoss.ReasonOf(m), true); return; }
        if (HasAttr(m, "Neemle.OrmLite.NotAColumnAttribute")) return;
        var reference = FirstAttr(m, "Neemle.OrmLite.ReferenceAttribute");
        if (ChildTableOf(m.Type) is { } child) { AddCollection(m, child, reference); return; }
        if (m.Type is INamedTypeSymbol target && (reference is not null || HasAttr(target, TableAttribute)))
        {
            AddNavigation(m, target, reference);
            return;
        }
        if (IsUnresolved(m.Type)) return;
        AddColumn(m);
    }

    private static INamedTypeSymbol? ChildTableOf(ITypeSymbol type) =>
        type is INamedTypeSymbol coll && coll.IsGenericType
        && (coll.Name == "List" || coll.Name == "IReadOnlyList" || coll.Name == "IList")
        && coll.TypeArguments.Length == 1 && coll.TypeArguments[0] is INamedTypeSymbol child
        && HasAttr(child, TableAttribute)
            ? child
            : null;

    private static string ReferenceName(AttributeData? reference, string fallback) =>
        (reference is { ConstructorArguments.Length: > 0 } ? reference.ConstructorArguments[0].Value as string : null)
        ?? fallback;

    private void AddCollection(IPropertySymbol m, INamedTypeSymbol child, AttributeData? reference)
    {
        if (Found.Exclude(m, AccessRules.NavigationIssue(m, View, Found.Home))) return;
        var link = FirstAttr(m, "Neemle.OrmLite.ManyToManyAttribute");
        if (link is { ConstructorArguments.Length: >= 3 })
            Links.Add(ManyToManyOf(m, child, link));
        else
            Collections.Add((m.Name, child, ReferenceName(reference, Type.Name + "Id")));
    }

    private void AddNavigation(IPropertySymbol m, INamedTypeSymbol target, AttributeData? reference)
    {
        var fkName = ReferenceName(reference, m.Name + "Id");
        Navs.Add(new NavRef(m.Name, target, fkName, AccessRules.NavigationIssue(m, View, Found.Home), LocationOf(m)));
    }

    private void AddColumn(IPropertySymbol m)
    {
        var classified = ColumnTypes.Classify(m.Type);
        if (classified is null)
        {
            AddUnmapped(m);
            return;
        }
        var declared = ReadColumn(m, classified.Value);
        if (declared is null) return;
        Props.Add(declared);
        foreach (var index in AttrsOf(m, IndexReader.IndexAttributeName))
            OnProperties.Add((declared, index));
    }

    private void AddUnmapped(IPropertySymbol m)
    {
        UnmappedProps.Add(
            new Unmapped(m.Name, m.Type.ToDisplayString(), LocationOf(m), UnmappedNote.Of(m, SetsRequired)));
        if (KeyLoss.IsKey(m)) Found.LostKeys.Add(m.Name);
    }

    private Prop? ReadColumn(IPropertySymbol m, (string read, string type, string cs) shape)
    {
        var (read, type, cs) = shape;
        var keyed = HasAttr(m, "Neemle.OrmLite.KeyAttribute");
        var isKey = keyed || m.Name == "Id";
        if (Found.Exclude(m, AccessRules.ColumnIssue(m, View, Found.Home), isKey)) return null;
        var (precision, scale) = NumericShape(m);
        var inInit = m.SetMethod!.IsInitOnly || (m.IsRequired && !SetsRequired);
        return new Prop(
            m.Name, ColumnOf(m), type, read, isKey, IsNullable(m), cs, precision, scale, isKey && !keyed, inInit,
            NavigationFacts.IsNullableValue(m));
    }

    private static ManyToMany ManyToManyOf(IPropertySymbol m, INamedTypeSymbol child, AttributeData attribute)
    {
        var (oTable, oKeyCol, oKeyCs, oFull) = EntityMeta(child);
        var args = attribute.ConstructorArguments;
        return new ManyToMany(m.Name, child.Name, oFull, oTable, oKeyCol, oKeyCs,
            args[0].Value as string ?? "", args[1].Value as string ?? "", args[2].Value as string ?? "",
            SoftDeleteReader.SoftColumnOf(child));
    }

    private static (string Table, string KeyCol, string KeyCs, string Full) EntityMeta(INamedTypeSymbol t)
    {
        var table = AttrArg(t, TableAttribute) ?? Snake(t.Name);
        var members = PropertiesOf(t);
        var keyProp = members.FirstOrDefault(p => HasAttr(p, "Neemle.OrmLite.KeyAttribute"))
                      ?? members.FirstOrDefault(p => p.Name == "Id");
        var keyCol = keyProp is null ? "id" : ColumnOf(keyProp);
        var keyCs = keyProp is null
            ? "global::System.Guid"
            : (ColumnTypes.Classify(keyProp.Type)?.cs ?? "global::System.Guid");
        return (table, keyCol, keyCs, FullNameOf(t));
    }
}
