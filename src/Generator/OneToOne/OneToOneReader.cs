using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using static Neemle.OrmLite.Generator.SymbolFacts;

namespace Neemle.OrmLite.Generator;

internal static class OneToOneReader
{
    private const string Prefix = "Neemle.OrmLite.";

    internal static bool Explicit(IPropertySymbol property) =>
        HasAttr(property, Prefix + "OneToOneAttribute") || HasAttr(property, Prefix + "InverseOneToOneAttribute")
        || HasAttr(property, Prefix + "OneToOneUniqueAttribute");

    internal static ImmutableArray<OneToOneIntent> Read(INamedTypeSymbol type) =>
        PropertiesOf(type).Where(Explicit).Select(ReadProperty).ToImmutableArray();

    private static OneToOneIntent ReadProperty(IPropertySymbol property)
    {
        var owner = FirstAttr(property, Prefix + "OneToOneAttribute");
        var inverse = FirstAttr(property, Prefix + "InverseOneToOneAttribute");
        var target = property.Type is INamedTypeSymbol named ? FullNameOf(named) : "";
        return new OneToOneIntent(property.Name, target, Argument(owner ?? inverse, 0), Argument(owner, 1),
            owner is not null)
        {
            UniqueName = AttrArg(property, Prefix + "OneToOneUniqueAttribute") ?? "",
            Assignable = Assignable(property) && Attributes(property, owner, inverse),
            Location = LocationOf(property),
        };
    }

    private static bool Assignable(IPropertySymbol property) =>
        property.Type.IsReferenceType && IsNullable(property) && !property.IsStatic && !property.IsIndexer
        && property.GetMethod?.DeclaredAccessibility == Accessibility.Public
        && property.SetMethod is { DeclaredAccessibility: Accessibility.Public, IsInitOnly: false };

    private static bool Attributes(IPropertySymbol property, AttributeData? owner, AttributeData? inverse) =>
        !HasAttr(property, Prefix + "NotAColumnAttribute") && !(owner is not null && inverse is not null)
        && !HasAttr(property, Prefix + "CalculatedLowerAttribute")
        && !HasAttr(property, Prefix + "ManyToManyAttribute") && Reference(property, owner);

    private static bool Reference(IPropertySymbol property, AttributeData? owner) =>
        owner is null || AttrArg(property, Prefix + "ReferenceAttribute") is not { } reference
        || reference == Argument(owner, 0);

    private static string Argument(AttributeData? data, int index) =>
        data is not null && data.ConstructorArguments.Length > index
            ? data.ConstructorArguments[index].Value as string ?? "" : "";

    internal static void Visit(EntityScan scan, IPropertySymbol property)
    {
        if (!HasAttr(property, Prefix + "OneToOneAttribute") || property.Type is not INamedTypeSymbol target) return;
        var fk = AttrArg(property, Prefix + "OneToOneAttribute") ?? "";
        scan.Navs.Add(new EntityScan.NavRef(property.Name, target, fk, null, LocationOf(property)));
    }
}
