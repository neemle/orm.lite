using System.Collections.Concurrent;
using System.Data.Common;

namespace Neemle.OrmLite;

public static class OneToOneReadGuard
{
    private static readonly ConcurrentDictionary<Type, Delegate> Maps = new();

    public static void Register<T>(Func<DbDataReader, T> generatedMap) where T : class
    {
        ArgumentNullException.ThrowIfNull(generatedMap);
        if (!Maps.TryAdd(typeof(T), generatedMap) && !Maps[typeof(T)].Equals(generatedMap))
            throw new OneToOneDeclarationException(OneToOneError.ConflictingConstraint, typeof(T).FullName);
    }

    public static void Require(object entity, string navigation) => OneToOneProvenance.Require(entity, navigation);

    internal static bool Eligible(Type type, Delegate map) =>
        Maps.TryGetValue(type, out var registered) && registered.Equals(map);

    internal static IEnumerable<EntityDescriptor> Descriptors =>
        Maps.Keys.Select(OrmMeta.ReadDescriptor).OfType<EntityDescriptor>();
}
