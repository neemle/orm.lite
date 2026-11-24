using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// The extra sentences of an ORMLITE001 report that tell what else the unmapped property costs the entity.
/// An empty text leaves the message exactly as it was before the sentences existed.
/// </summary>
internal static class UnmappedNote
{
    private const string KeySentence =
        " The property is a key, so no keyed get, update or delete is generated for the entity.";

    private const string RequiredSentence =
        " The property is required, so the entity cannot be built from a row and its Map throws NotSupportedException.";

    /// <summary>
    /// The sentences for the property: a key loses the keyed api, a required member that no constructor satisfies
    /// (no [SetsRequiredMembers] on the constructor the generated code calls) makes the entity unbuildable.
    /// </summary>
    internal static string Of(IPropertySymbol property, bool constructorSetsRequired) =>
        (KeyLoss.IsKey(property) ? KeySentence : "")
        + (property.IsRequired && !constructorSetsRequired ? RequiredSentence : "");
}
