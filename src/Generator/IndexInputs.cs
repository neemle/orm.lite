using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace Neemle.OrmLite.Generator;

/// <summary>
/// Reads the inputs of an [Index] declaration that the attribute lets through but that must not make the generator
/// throw: a null array of terms, a whole number that does not fit 64 bits and a decimal number in a Where condition.
/// </summary>
internal static class IndexInputs
{
    private static readonly Regex DecimalCondition = new Regex(
        @"^\s*\w+\s*(?:<=|>=|<>|<|>|=)\s*-?\d+\.\d+\s*$", RegexOptions.CultureInvariant);

    /// <summary>The terms written in the constructor: none for no argument and for a null array.</summary>
    internal static List<string> WrittenTerms(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length == 0) return [];
        var argument = attribute.ConstructorArguments[0];
        if (argument.Kind != TypedConstantKind.Array || argument.IsNull) return [];
        return argument.Values.Select(v => v.Value as string ?? "").ToList();
    }

    /// <summary>The problem of a whole-number literal, or null when it fits a 64-bit integer.</summary>
    internal static string? NumberProblem(string literal) =>
        long.TryParse(literal, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _)
            ? null
            : "the number " + literal + " does not fit a 64-bit integer, the range a Where condition compares with";

    /// <summary>The problem text for a condition that the grammar cannot read.</summary>
    internal static string Unreadable(string condition) =>
        DecimalCondition.IsMatch(condition)
            ? "the condition '" + condition.Trim() + "' in Where compares with a decimal number; a Where condition "
                + "compares with a whole number, a text or a flag, so write the bound as a whole number of the "
                + "smallest unit or leave it out of the index"
            : "cannot read the condition '" + condition.Trim() + "' in Where; a condition is a column compared with "
                + "a text, number or flag by one of = <> < <= > >=, or a column that is null or is not null, "
                + "joined by and";
}
