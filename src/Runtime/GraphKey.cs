namespace Neemle.OrmLite;

public readonly record struct GraphKey<TKey>(bool HasValue, TKey Value) where TKey : notnull;
