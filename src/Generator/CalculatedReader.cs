using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using CalculatedProp = Neemle.OrmLite.Generator.OrmGenerator.CalculatedProp;
using Prop = Neemle.OrmLite.Generator.OrmGenerator.Prop;
using static Neemle.OrmLite.Generator.SymbolFacts;

namespace Neemle.OrmLite.Generator;

internal static class CalculatedReader
{
    internal static ImmutableArray<CalculatedProp> Read(EntityScan scan)
    {
        var result = ImmutableArray.CreateBuilder<CalculatedProp>();
        foreach (var property in scan.Calculated)
        {
            var source = AttrArg(property, "Neemle.OrmLite.CalculatedLowerAttribute") ?? "";
            var physical = scan.Props.FirstOrDefault(p => p.Name == source);
            var issue = Issue(scan, property, physical, result);
            if (issue is not null) { scan.Found.Add(property.Name, issue, LocationOf(property)); continue; }
            var shape = new Prop(property.Name, ColumnOf(property), "Text", "String", false,
                IsNullable(property), "string", InInit: property.SetMethod!.IsInitOnly || property.IsRequired);
            result.Add(new CalculatedProp(shape, source));
        }
        return result.ToImmutable();
    }

    private static string? Issue(EntityScan scan, IPropertySymbol property, Prop? source,
        ImmutableArray<CalculatedProp>.Builder accepted)
    {
        if (!AccessRules.IsMappable(property)) return "a calculated field must be a public assignable property";
        if (AccessRules.ColumnIssue(property, true, scan.Found.Home) is { } access) return access;
        return ShapeIssue(property, source) ?? RoleIssue(property) ?? AliasIssue(scan, property, accepted);
    }

    private static string? ShapeIssue(IPropertySymbol property, Prop? source)
    {
        if (property.Type.SpecialType != SpecialType.System_String)
            return "a calculated Lower field must be text";
        if (source is not { Read: "String" })
            return "a calculated Lower source must be a physical text property";
        return source.Nullable && !IsNullable(property)
            ? "a nullable source requires a nullable calculated field" : null;
    }

    private static string? RoleIssue(IPropertySymbol property)
    {
        var roles = new[] { "KeyAttribute", "IndexAttribute", "NotAColumnAttribute" };
        return property.Name == "Id" || roles.Any(role => HasAttr(property, "Neemle.OrmLite." + role))
            ? "a calculated field cannot declare a key, index or NotAColumn role" : null;
    }

    private static string? AliasIssue(EntityScan scan, IPropertySymbol property,
        ImmutableArray<CalculatedProp>.Builder accepted)
    {
        var alias = ColumnOf(property);
        return string.IsNullOrWhiteSpace(alias) || scan.Props.Any(p => Same(p.Column, alias))
            || accepted.Any(p => Same(p.Property.Column, alias))
                ? "a calculated field needs a distinct nonempty read alias" : null;
    }

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
