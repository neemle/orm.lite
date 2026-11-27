namespace Neemle.OrmLite;

public sealed class GraphSaveResult
{
    private readonly IReadOnlyDictionary<object, GraphSaveResultEntry> _entries;
    internal GraphSaveResult(IReadOnlyDictionary<object, GraphSaveResultEntry> entries) =>
        _entries = entries.ToDictionary(p => p.Key, p => p.Value, ReferenceEqualityComparer.Instance);

    public GraphSaveOutcome Outcome => GraphSaveOutcome.Committed;
    public bool WasWritten<T>(T entity) where T : class =>
        entity is not null && _entries.TryGetValue(entity, out var entry) && entry.Written;

    public TKey BoundForeignKey<T, TKey>(T entity, string property) where T : class where TKey : notnull
    {
        if (entity is not null && property is not null && _entries.TryGetValue(entity, out var entry)
            && entry.Bindings.TryGetValue(property, out var value) && value is TKey key) return key;
        throw new GraphSaveException(GraphSaveError.NoPublishedBinding, Outcome, committedResult: this);
    }
}
