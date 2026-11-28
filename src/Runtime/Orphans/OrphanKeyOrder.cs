namespace Neemle.OrmLite;

internal sealed class OrphanKeyOrder<TKey> : IComparer<TKey> where TKey : notnull
{
    internal static OrphanKeyOrder<TKey> Instance { get; } = new();

    public int Compare(TKey? left, TKey? right) => left is string a && right is string b
        ? StringComparer.Ordinal.Compare(a, b) : Comparer<TKey>.Default.Compare(left!, right!);
}
