namespace Neemle.OrmLite;

internal static class JoinAliases
{
    internal const string InternalPrefix = "__orm_join_";

    internal static void Validate(string alias, ISqlProvider provider, JoinError error)
    {
        if (string.IsNullOrEmpty(alias)) throw new JoinReadException(error);
        if (!Letter(alias[0]) || alias.Any(c => !Letter(c) && !char.IsAsciiDigit(c)))
            throw new JoinReadException(error);
        if (alias.StartsWith(InternalPrefix, StringComparison.OrdinalIgnoreCase)) throw new JoinReadException(error);
        if (Reserved(alias)) throw new JoinReadException(error);
        if (provider.Capabilities.MaxIdentifierBytes is { } limit && alias.Length > limit)
            throw new JoinReadException(error);
    }

    private static bool Letter(char c) => char.IsAsciiLetter(c) || c == '_';
    private static bool Reserved(string alias) => alias.Length == 2
        && (alias[0] is 'j' or 'J') && alias[1] is >= '0' and <= '3';
}
