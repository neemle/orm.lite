using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using static Neemle.OrmLite.Generator.SymbolFacts;

namespace Neemle.OrmLite.Generator;

internal static class ForeignKeyReader
{
    internal static ImmutableArray<ForeignKeyIntent> Read(INamedTypeSymbol type) =>
        PropertiesOf(type).SelectMany(p => ReadProperty(p)).ToImmutableArray();

    private static ImmutableArray<ForeignKeyIntent> ReadProperty(IPropertySymbol property)
    {
        var result = ImmutableArray.CreateBuilder<ForeignKeyIntent>();
        foreach (var a in property.GetAttributes())
        {
            var name = a.AttributeClass?.ToDisplayString();
            if (name == "Neemle.OrmLite.ForeignKeyAttribute")
                result.Add(Intent(property, a, false));
            if (name == "Neemle.OrmLite.LinkForeignKeyAttribute")
                result.Add(Intent(property, a, true));
        }
        return result.ToImmutable();
    }

    private static ForeignKeyIntent Intent(IPropertySymbol property, AttributeData attribute, bool link)
    {
        var args = attribute.ConstructorArguments;
        var offset = link ? 1 : 0;
        var name = args.Length > offset ? args[offset].Value as string ?? "" : "";
        var action = args.Length > offset + 1 && args[offset + 1].Value is int value ? value : -1;
        int? end = link ? args.Length > 0 && args[0].Value is int side ? side : -1 : null;
        return new ForeignKeyIntent(property.Name, name, action, end,
            attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? LocationOf(property));
    }
}
