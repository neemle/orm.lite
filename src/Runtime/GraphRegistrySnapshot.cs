namespace Neemle.OrmLite;

internal sealed class GraphRegistrySnapshot(IReadOnlyDictionary<Type, GraphEntitySnapshot> entities)
{
    internal GraphEntitySnapshot Get(Type type) => entities.TryGetValue(type, out var entity) ? entity
        : throw new GraphRequestException(GraphRequestError.UnregisteredEntity);
}
