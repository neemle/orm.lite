using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Neemle.OrmLite.Generator.SymbolFacts;

namespace Neemle.OrmLite.Generator.GraphSave;

internal static class SaveSafetyReader
{
    internal static SaveSafetyFacts Read(EntityScan scan)
    {
        var properties = PropertiesOf(scan.Type).ToLookup(p => p.Name);
        var scalars = scan.Props.All(p => Safe(properties[p.Name].SingleOrDefault()));
        var references = scan.Navs.Select(n => n.Nav)
            .Where(n => Safe(properties[n].SingleOrDefault())).ToImmutableArray();
        var lists = scan.Collections.Select(c => c.Nav).Concat(scan.Links.Select(l => l.Nav))
            .Where(n => SafeList(properties[n].SingleOrDefault())).Distinct().ToImmutableArray();
        return new SaveSafetyFacts(scalars, references, lists);
    }

    private static bool Safe(IPropertySymbol? property) =>
        property is not null && Ordinary(property) && TrivialSyntax(property);

    private static bool Ordinary(IPropertySymbol property) =>
        !property.IsStatic && !property.IsIndexer && !property.IsVirtual
        && !property.IsOverride && !property.IsAbstract
        && property.GetMethod is { DeclaredAccessibility: Accessibility.Public, IsExtern: false };

    private static bool TrivialSyntax(IPropertySymbol property) => property.DeclaringSyntaxReferences.Any(r =>
        r.GetSyntax() switch
        {
            PropertyDeclarationSyntax { ExpressionBody: null, AccessorList: { } accessors } =>
                accessors.Accessors.All(a => a.Body is null && a.ExpressionBody is null),
            ParameterSyntax { Parent: ParameterListSyntax { Parent: RecordDeclarationSyntax } } =>
                property.GetMethod?.IsImplicitlyDeclared == true,
            _ => false,
        });

    private static bool SafeList(IPropertySymbol? property) => Safe(property)
        && property!.Type is INamedTypeSymbol { Name: "List", Arity: 1 } type
        && type.ContainingNamespace.ToDisplayString() == "System.Collections.Generic";
}
