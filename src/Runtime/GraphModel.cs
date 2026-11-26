namespace Neemle.OrmLite;

public static class GraphModel
{
    private static readonly object Gate = new();
    private static readonly Dictionary<Type, IGraphEntityBinding> Bindings = [];
    private static int _active;

    public static void Register<T>(GraphEntityBinding<T> binding) where T : class
    {
        ArgumentNullException.ThrowIfNull(binding);
        lock (Gate)
        {
            if (Bindings.TryGetValue(typeof(T), out var old))
            {
                if (old.Equivalent(binding)) return;
                throw new GraphRequestException(GraphRequestError.ConflictingBinding);
            }
            if (_active != 0) throw new GraphRequestException(GraphRequestError.ConflictingBinding);
            Bindings.Add(typeof(T), binding);
        }
    }

    internal static IGraphEntityBinding Find(Type type)
    {
        lock (Gate) return Bindings.TryGetValue(type, out var binding) ? binding
            : throw new GraphRequestException(GraphRequestError.UnregisteredEntity);
    }

    internal static GraphRegistrySnapshot Snapshot()
    {
        lock (Gate) return new GraphRegistrySnapshot(Bindings.ToDictionary(p => p.Key,
            p => new GraphEntitySnapshot(p.Value)));
    }

    internal static GraphRegistrationLease Enter()
    { lock (Gate) { _active++; return new GraphRegistrationLease(); } }

    internal static void Exit() { lock (Gate) _active--; }
}
