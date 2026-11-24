using System;

namespace Neemle.Orm.Lite;

/// <summary>
/// The singular form that names the Add and Remove methods of a many-to-many list (AddTagAsync for Tags). It strips
/// a plural s, but leaves a name that already ends like a singular (Status, Address, Analysis) alone and turns
/// -sses into -ss (Addresses to Address). It has no word list, so an irregular plural keeps the plain rule.
/// </summary>
internal static class Singular
{
    internal static string Of(string name)
    {
        if (name.EndsWith("sses", StringComparison.Ordinal)) return name.Substring(0, name.Length - 2);
        if (name.EndsWith("ss", StringComparison.Ordinal) || name.EndsWith("us", StringComparison.Ordinal)
            || name.EndsWith("is", StringComparison.Ordinal))
            return name;
        return name.EndsWith("s", StringComparison.Ordinal) ? name.Substring(0, name.Length - 1) : name;
    }
}
