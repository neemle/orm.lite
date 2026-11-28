namespace Neemle.OrmLite;

internal sealed record OrphanFrozenWrite<TKey>(TKey Key, OrphanPresentIntent Intent, IReadOnlyList<object?> Values)
    where TKey : notnull;
