using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Entity = Neemle.OrmLite.Generator.OrmGenerator.Entity;
using static Neemle.OrmLite.Generator.SymbolFacts;

namespace Neemle.OrmLite.Generator;

internal static class RelationPolicyReader
{
    private const string ModeAttribute = "Neemle.OrmLite.RelationLoadAttribute";
    private const string LimitsAttribute = "Neemle.OrmLite.RelationLoadLimitsAttribute";
    private static readonly string[] Dimensions =
        ["MaxDepth", "MaxPaths", "MaxNodes", "MaxRoots", "MaxRelatedRows", "MaxStatements"];

    internal static Entity Read(INamedTypeSymbol type, Entity entity)
    {
        var declarations = new List<PolicyDeclaration>();
        var issues = new List<PolicyIssue>();
        foreach (var property in PropertiesOf(type)) ReadProperty(property, declarations, issues);
        var limits = ReadLimits(type, declarations, issues);
        if (declarations.Count == 0 && type.GetAttributes().All(a => !Is(a, LimitsAttribute))) return entity;
        return entity with { LoadingPolicy = new PolicyMetadata(declarations.ToImmutableArray(), limits,
            issues.ToImmutableArray()) };
    }

    private static void ReadProperty(IPropertySymbol property, List<PolicyDeclaration> declarations,
        List<PolicyIssue> issues)
    {
        var attributes = property.GetAttributes().Where(a => Is(a, ModeAttribute)).ToArray();
        if (attributes.Length == 0) return;
        var mode = attributes[0].ConstructorArguments.FirstOrDefault().Value;
        if (attributes.Length != 1 || mode is not int value || value < 0 || value > 2)
            issues.Add(new PolicyIssue(property.Name, "invalid or conflicting relation load mode", LocationOf(property))
            );
        else declarations.Add(new PolicyDeclaration(property.Name, value, LocationOf(property)));
    }

    private static ImmutableArray<int> ReadLimits(INamedTypeSymbol type, List<PolicyDeclaration> declarations,
        List<PolicyIssue> issues)
    {
        var attributes = type.GetAttributes().Where(a => Is(a, LimitsAttribute)).ToArray();
        if (attributes.Length == 0 && declarations.All(p => p.Mode == 0)) return [];
        var values = attributes.Length == 1 ? attributes[0].NamedArguments.ToDictionary(p => p.Key, p => p.Value.Value)
            : new Dictionary<string, object?>();
        var limits = Dimensions.Select(name => values.TryGetValue(name, out var value) && value is int count
            ? count : 0).ToImmutableArray();
        if (attributes.Length != 1 || limits.Any(value => value <= 0))
            issues.Add(new PolicyIssue(type.Name, "all six positive named load limits are required", LocationOf(type)));
        return limits;
    }

    private static bool Is(AttributeData attribute, string name) => attribute.AttributeClass?.ToDisplayString() == name;
}
