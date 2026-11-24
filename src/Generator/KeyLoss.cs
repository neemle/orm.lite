using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite;

/// <summary>
/// A property marked [Key] that the generator skips without a column: it is get-only, set-only, not public, or has an
/// unmapped type. Such a key must still count as a key of the entity, because an entity that lost one of its keys
/// cannot offer a keyed get, update or delete (they would address rows by the remaining key alone).
/// </summary>
internal static class KeyLoss
{
    private const string KeyAttribute = "Neemle.OrmLite.KeyAttribute";

    private const string NotAColumnAttribute = "Neemle.OrmLite.NotAColumnAttribute";

    private const string Consequence =
        "the property is not mapped, and with a key lost no keyed get, update or delete is generated for the entity. ";

    internal static bool IsKey(ISymbol member) => Has(member, KeyAttribute);

    /// <summary>
    /// Why the property is a key the generator cannot map, or null when it is not a key, is not an instance property,
    /// or is excluded on purpose with [NotAColumn] (which stays pinned: the property is not part of the model).
    /// </summary>
    internal static string? ReasonOf(IPropertySymbol property)
    {
        if (property.IsStatic || property.IsIndexer || !IsKey(property) || Has(property, NotAColumnAttribute))
            return null;
        if (property.DeclaredAccessibility != Accessibility.Public)
            return Text(
                "it is declared " + AccessWord(property.DeclaredAccessibility)
                + " and only public properties are mapped",
                "Make it public");
        if (property.GetMethod is null)
            return Text("it has no get accessor, so the generated code cannot read it when it writes a row",
                "Give it a public get accessor");
        return property.SetMethod is null
            ? Text("it has no set accessor (it is get-only, or its setter is private, or it is declared in another "
                + "assembly where the setter is not accessible), so the generated code cannot assign it when it "
                + "reads a row", "Give it a public set or init accessor")
            : null;
    }

    private static string Text(string cause, string fix) =>
        "it is a [Key] but " + cause + "; " + Consequence + fix + ", or mark the property [NotAColumn]";

    private static string AccessWord(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        Accessibility.Internal => "internal",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => "not public",
    };

    private static bool Has(ISymbol member, string attribute)
    {
        foreach (var data in member.GetAttributes())
            if (data.AttributeClass?.ToDisplayString() == attribute) return true;
        return false;
    }
}
