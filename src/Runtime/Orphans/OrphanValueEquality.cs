namespace Neemle.OrmLite;

internal static class OrphanValueEquality
{
    internal static bool Same(IReadOnlyList<object?> left, IReadOnlyList<object?> right)
    {
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++)
            if (!Value(left[i], right[i])) return false;
        return true;
    }

    private static bool Value(object? left, object? right) => left is DateTime a && right is DateTime b
        ? a.Ticks == b.Ticks && a.Kind == b.Kind : Equals(left, right);
}
