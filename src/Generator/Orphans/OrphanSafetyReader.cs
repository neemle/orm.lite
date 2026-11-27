using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Neemle.OrmLite.Generator.SymbolFacts;

namespace Neemle.OrmLite.Generator.Orphans;

internal static class OrphanSafetyReader
{
    internal static OrphanSafetyFacts Read(EntityScan scan)
    {
        var properties = PropertiesOf(scan.Type).ToLookup(p => p.Name);
        var physical = scan.Props.All(p => Safe(Unique(properties[p.Name])));
        var lists = scan.Collections.Select(c => c.Nav)
            .Where(n => SafeList(Unique(properties[n]))).Distinct().ToImmutableArray();
        return new(physical, lists);
    }

    private static IPropertySymbol? Unique(IEnumerable<IPropertySymbol> properties)
    {
        var candidates = properties.Take(2).ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static bool Safe(IPropertySymbol? property) => property is not null
        && !property.IsStatic && !property.IsIndexer && !property.IsVirtual && !property.IsOverride
        && !property.IsAbstract && property.GetMethod is
            { DeclaredAccessibility: Accessibility.Public, IsExtern: false } && Trivial(property);

    private static bool Trivial(IPropertySymbol property) => property.DeclaringSyntaxReferences.Any(r =>
        r.GetSyntax() switch
        {
            PropertyDeclarationSyntax { ExpressionBody: null, AccessorList: { } accessors } declaration =>
                !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
                && accessors.Accessors.All(a => a.Body is null && a.ExpressionBody is null),
            ParameterSyntax { Parent: ParameterListSyntax { Parent: RecordDeclarationSyntax } } =>
                property.GetMethod?.IsImplicitlyDeclared == true,
            _ => false,
        });

    private static bool SafeList(IPropertySymbol? property) => Safe(property)
        && property!.Type is INamedTypeSymbol { Name: "List", Arity: 1 } type
        && type.ContainingNamespace.ToDisplayString() == "System.Collections.Generic";
}
