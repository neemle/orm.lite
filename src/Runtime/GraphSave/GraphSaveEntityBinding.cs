using System.Data.Common;

namespace Neemle.OrmLite;

public sealed class GraphSaveEntityBinding<T, TKey> : IGraphSaveEntity where T : class where TKey : notnull
{
    private readonly Func<T, object?[]> _values;
    private readonly Func<T, TKey> _key;
    private readonly Func<DbDataReader, T> _map;
    private readonly IReadOnlyList<IGraphSaveRelation> _relations;
    private readonly EntityDescriptor _descriptor;
    private readonly EntityDescriptor _original;

    public GraphSaveEntityBinding(EntityDescriptor descriptor, Func<T, object?[]> values, Func<T, TKey> key,
        Func<DbDataReader, T> map, IReadOnlyList<GraphSaveRelationBinding<T>> relations)
    {
        if (descriptor is null || values is null || key is null || map is null || relations is null)
            throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        _original = descriptor;
        _descriptor = GraphSaveDescriptors.Freeze(descriptor);
        GraphSaveShapes.Key(typeof(TKey), _descriptor.SoleKey!);
        _values = values;
        _key = key;
        _map = map;
        if (relations.Any(r => r is null)) throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
        _relations = Array.AsReadOnly(relations.Select(r => r.Binding).ToArray());
        if (_relations.Select(r => r.Shape.Navigation).Distinct(StringComparer.Ordinal).Count() != relations.Count)
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
    }

    Type IGraphSaveEntity.EntityType => typeof(T);
    Type IGraphSaveEntity.KeyType => typeof(TKey);
    EntityDescriptor IGraphSaveEntity.Original => _original;
    EntityDescriptor IGraphSaveEntity.Descriptor => _descriptor;
    Delegate IGraphSaveEntity.Mapper => _map;
    bool IGraphSaveEntity.HasReadPolicy => PolicyReadHooks<T>.Snapshot is not null;
    IEqualityComparer<object> IGraphSaveEntity.KeyComparer { get; } = new GraphSaveKeyComparer<TKey>();
    IReadOnlyList<IGraphSaveRelation> IGraphSaveEntity.Relations => _relations;
    object IGraphSaveEntity.Key(object entity) => entity is T typed && _key(typed) is { } key
        ? key : throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);

    object?[] IGraphSaveEntity.Values(object entity)
    {
        if (entity is not T typed) throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
        var supplied = _values(typed);
        if (supplied is null) throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
        var values = (object?[])supplied.Clone();
        GraphSaveValues.Validate(_descriptor, values);
        return values;
    }

    bool IGraphSaveEntity.Equivalent(IGraphSaveEntity other) => other is GraphSaveEntityBinding<T, TKey> typed
        && ReferenceEquals(_original, typed._original) && GraphSaveDescriptors.Same(_descriptor, typed._descriptor)
        && _key.Equals(typed._key) && _map.Equals(typed._map) && _values.Equals(typed._values)
        && _relations.Count == typed._relations.Count
        && _relations.Zip(typed._relations).All(p => p.First.Equivalent(p.Second));
}
