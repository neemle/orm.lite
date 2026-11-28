namespace Neemle.OrmLite;

internal sealed record OrphanPrepared<TKey>(IReadOnlyList<TKey> Candidates,
    IReadOnlyList<OrphanFrozenWrite<TKey>> Present) where TKey : notnull;
