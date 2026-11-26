namespace Neemle.OrmLite;

internal sealed class GraphIdentityMap
{
    private readonly Dictionary<Type, Dictionary<object, object>> _entities = [];

    internal object Intern(IGraphEntityBinding binding, object value)
    {
        if (!_entities.TryGetValue(binding.EntityType, out var entities))
        {
            entities = new Dictionary<object, object>(binding.Comparer);
            _entities.Add(binding.EntityType, entities);
        }
        var key = binding.Key(value);
        if (entities.TryGetValue(key, out var existing)) return existing;
        entities.Add(key, value);
        return value;
    }
}
