using System;
using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// Small facts about key and foreign key properties that the navigation loaders depend on: whether a nullable
/// property is a Nullable of a value type (it has .Value) or a nullable reference (it has not), and whether the
/// value of one key type can be passed to or compared with another without a compile error.
/// </summary>
internal static class NavigationFacts
{
    private const string GlobalPrefix = "global::";

    internal static bool IsNullableValue(IPropertySymbol property) =>
        property.Type is INamedTypeSymbol { ConstructedFrom.SpecialType: SpecialType.System_Nullable_T };

    internal static string Short(string clrType) =>
        clrType.StartsWith(GlobalPrefix, StringComparison.Ordinal)
            ? clrType.Substring(GlobalPrefix.Length)
            : clrType;

    /// <summary>True when a foreign key of the first type can be passed where the second type is expected.</summary>
    internal static bool Passes(string from, string to) => from == to || Widens(Short(from), Short(to));

    /// <summary>True when the two types can stand on either side of == without a compile error.</summary>
    internal static bool Comparable(string left, string right) => Passes(left, right) || Passes(right, left);

    private static bool Widens(string from, string to) => from switch
    {
        "int" => to is "long" or "double" or "decimal",
        "long" => to is "double" or "decimal",
        _ => false,
    };
}
