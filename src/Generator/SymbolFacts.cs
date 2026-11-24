using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// Reads of attributes, names, locations and member chains on Roslyn symbols that the entity reading, the naming and
/// the index reading share. Nothing here knows the entity model.
/// </summary>
internal static class SymbolFacts
{
    internal const string GlobalPrefix = "global::";

    internal static bool HasAttr(ISymbol s, string name) =>
        s.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == name);

    internal static AttributeData? FirstAttr(ISymbol s, string name) =>
        s.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == name);

    internal static IEnumerable<AttributeData> AttrsOf(ISymbol s, string name) =>
        s.GetAttributes().Where(a => a.AttributeClass?.ToDisplayString() == name);

    internal static bool NamedBool(AttributeData a, string name) =>
        a.NamedArguments.Any(n => n.Key == name && n.Value.Value is true);

    internal static bool NamedBoolOrDefault(ISymbol s, string attribute, string name, bool fallback)
    {
        var a = FirstAttr(s, attribute);
        if (a is null) return fallback;
        foreach (var n in a.NamedArguments)
            if (n.Key == name && n.Value.Value is bool b) return b;
        return fallback;
    }

    internal static int NamedInt(AttributeData a, string name) =>
        a.NamedArguments.Where(n => n.Key == name).Select(n => n.Value.Value is int i ? i : 0).FirstOrDefault();

    internal static string? NamedString(AttributeData a, string name) =>
        a.NamedArguments.FirstOrDefault(n => n.Key == name).Value.Value as string;

    internal static (int Precision, int Scale) NumericShape(ISymbol s)
    {
        var a = FirstAttr(s, "Neemle.OrmLite.NumericAttribute");
        if (a is not { ConstructorArguments.Length: 2 }) return (0, 0);
        return (a.ConstructorArguments[0].Value is int p ? p : 0, a.ConstructorArguments[1].Value is int q ? q : 0);
    }

    internal static string? AttrArg(ISymbol s, string name)
    {
        var a = FirstAttr(s, name);
        return a is { ConstructorArguments.Length: > 0 } ? a.ConstructorArguments[0].Value as string : null;
    }

    internal static string Snake(string s)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (char.IsUpper(ch)) { if (i > 0) sb.Append('_'); sb.Append(char.ToLowerInvariant(ch)); }
            else sb.Append(ch);
        }
        return sb.ToString();
    }

    internal static string ColumnOf(ISymbol m) => AttrArg(m, "Neemle.OrmLite.ColumnAttribute") ?? Snake(m.Name);

    internal static Location LocationOf(ISymbol symbol) =>
        symbol.Locations.FirstOrDefault(l => l.IsInSource) ?? Location.None;

    /// <summary>Where an attribute is applied, falling back to the type and then to no location.</summary>
    internal static Location AttributeLocation(AttributeData a, INamedTypeSymbol type) =>
        a.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? type.Locations.FirstOrDefault() ?? Location.None;

    internal static string FullNameOf(INamedTypeSymbol type) =>
        GlobalPrefix + type.OriginalDefinition.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString();

    internal static bool IsUnresolved(ITypeSymbol t)
    {
        var u = t;
        if (t is INamedTypeSymbol nt && nt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T)
            u = nt.TypeArguments[0];
        return u.TypeKind == TypeKind.Error;
    }

    internal static bool IsNullable(IPropertySymbol m) =>
        m.NullableAnnotation == NullableAnnotation.Annotated
        || (m.Type is INamedTypeSymbol nt && nt.ConstructedFrom.SpecialType == SpecialType.System_Nullable_T);

    internal static IEnumerable<INamedTypeSymbol> EnclosingTypes(INamedTypeSymbol s)
    {
        for (var t = s; t is not null; t = t.ContainingType) yield return t;
    }

    internal static List<INamedTypeSymbol> ChainOf(INamedTypeSymbol s)
    {
        var chain = new List<INamedTypeSymbol>();
        for (var t = s; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType) chain.Add(t);
        return chain;
    }

    internal static List<IPropertySymbol> PropertiesOf(INamedTypeSymbol s)
    {
        var chain = ChainOf(s);
        var winners = new Dictionary<string, IPropertySymbol>();
        foreach (var t in chain)
            foreach (var p in t.GetMembers().OfType<IPropertySymbol>())
                if (!p.IsIndexer && !winners.ContainsKey(p.Name)) winners[p.Name] = p;
        var ordered = new List<IPropertySymbol>();
        for (var i = chain.Count - 1; i >= 0; i--)
            foreach (var p in chain[i].GetMembers().OfType<IPropertySymbol>())
                if (!p.IsIndexer && SymbolEqualityComparer.Default.Equals(winners[p.Name], p)) ordered.Add(p);
        return ordered;
    }
}
