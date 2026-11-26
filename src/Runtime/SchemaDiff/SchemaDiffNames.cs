namespace Neemle.OrmLite;

internal static class SchemaDiffNames
{
    internal static string Required(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\0'))
            throw new SchemaDiffException(SchemaDiffError.InvalidRequest);
        return value;
    }

    internal static IReadOnlyList<T> Limited<T>(IEnumerable<T> values, int limit)
    {
        ArgumentNullException.ThrowIfNull(values);
        var copied = new List<T>();
        foreach (var value in values)
        {
            if (copied.Count >= limit) throw new SchemaDiffException(SchemaDiffError.LimitExceeded);
            copied.Add(value);
        }
        return copied.AsReadOnly();
    }

    internal static void Unique(IEnumerable<string> values)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in values)
            if (!names.Add(Required(name))) throw new SchemaDiffException(SchemaDiffError.Ownership);
    }
}
