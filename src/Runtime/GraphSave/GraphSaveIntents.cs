namespace Neemle.OrmLite;

public sealed class GraphSaveIntents
{
    private readonly IReadOnlyDictionary<object, GraphSaveIntent> _items;
    private GraphSaveIntents(IReadOnlyDictionary<object, GraphSaveIntent> items) => _items = items;
    public static GraphSaveIntents Empty { get; } = new(new Dictionary<object, GraphSaveIntent>());

    public GraphSaveIntents With<T>(T entity, GraphSaveIntent intent) where T : class
    {
        if (entity is null) throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        GraphSaveRefusal.Intent(intent);
        if (_items.TryGetValue(entity, out var prior) && prior != intent)
            throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        var items = _items.ToDictionary(p => p.Key, p => p.Value, ReferenceEqualityComparer.Instance);
        items[entity] = intent;
        return new GraphSaveIntents(items);
    }

    internal IReadOnlyDictionary<object, GraphSaveIntent> Items => _items;
}
