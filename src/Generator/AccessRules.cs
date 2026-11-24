using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// Whether the generated code can reach a property and its accessors, and the sentence that says why not. The
/// generated code lives in the assembly of the entity or in another one, which changes what internal means.
/// </summary>
internal static class AccessRules
{
    private const string NotMapped = "the property is not mapped and no loader that reads it is generated";

    private const string NavigationNotMapped = "the navigation is not mapped and no loader for it is generated";

    internal static bool IsMappable(IPropertySymbol m) =>
        !m.IsStatic && !m.IsIndexer && m.GetMethod is not null && m.SetMethod is not null
        && m.DeclaredAccessibility == Accessibility.Public;

    internal static bool Usable(Accessibility accessibility, bool local = true) =>
        accessibility == Accessibility.Public
        || (local && accessibility is Accessibility.Internal or Accessibility.ProtectedOrInternal);

    internal static string? ColumnIssue(IPropertySymbol m, bool view, IAssemblySymbol? home)
    {
        var local = Reaches(m, home);
        var set = m.SetMethod!;
        if (!Usable(set.DeclaredAccessibility, local))
            return AccessorText(set.IsInitOnly ? "init" : "set", set)
                + ", so the generated code cannot assign it when it reads a row; " + NotMapped
                + ". Make the accessor public, internal or init, or mark the property [NotAColumn]";
        var get = m.GetMethod!;
        return view || Usable(get.DeclaredAccessibility, local)
            ? null
            : AccessorText("get", get)
                + ", so the generated code cannot read it when it writes a row; " + NotMapped
                + ". Make the accessor public or internal, or mark the property [NotAColumn]";
    }

    internal static string? NavigationIssue(IPropertySymbol m, bool view, IAssemblySymbol? home) =>
        view ? null : NavigationIssue(m, home);

    private static string? NavigationIssue(IPropertySymbol m, IAssemblySymbol? home)
    {
        var set = m.SetMethod!;
        if (!Usable(set.DeclaredAccessibility, Reaches(m, home)))
            return AccessorText(set.IsInitOnly ? "init" : "set", set)
                + ", so a generated loader cannot assign it; " + NavigationNotMapped
                + ". Make the accessor public or internal, or mark the property [NotAColumn]";
        return set.IsInitOnly
            ? "it is init-only, so a generated loader cannot assign it after the entity is built; "
                + NavigationNotMapped + ". Give it a public or internal set accessor, "
                + "or mark the property [NotAColumn]"
            : null;
    }

    private static string AccessWord(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => "not accessible",
    };

    private static string AccessorText(string kind, IMethodSymbol accessor) =>
        "its " + kind + " accessor is " + AccessWord(accessor.DeclaredAccessibility);

    private static bool Reaches(ISymbol member, IAssemblySymbol? home) =>
        member.ContainingAssembly is not { } owner || home is null || owner.GivesAccessTo(home);
}
