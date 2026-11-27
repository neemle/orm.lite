namespace Neemle.OrmLite;

public static class OrphanModel
{
    private static readonly object Sync = new();
    private static readonly Dictionary<Type, IOrphanEntity> Entities = [];
    private static readonly Dictionary<(Type Parent, Type Child, string Navigation), IOrphanCollection>
        Collections = [];

    public static void RegisterEntity<T, TKey>(OrphanEntityBinding<T, TKey> binding)
        where T : class where TKey : notnull
    {
        if (binding is null) throw OrphanRefusal.At(OrphanError.InvalidRequest);
        lock (Sync) Register(binding);
    }

    public static void RegisterCollection<TParent, TChild, TParentKey, TChildKey>(
        OrphanEntityBinding<TParent, TParentKey> parent, OrphanEntityBinding<TChild, TChildKey> child,
        OrphanCollectionShape shape)
        where TParent : class where TChild : class where TParentKey : notnull where TChildKey : notnull
    {
        if (parent is null || child is null || shape is null)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        lock (Sync)
        {
            var candidate = new OwnedCollection<TParent, TChild, TParentKey, TChildKey>(parent, child, shape);
            ((IOrphanCollection)candidate).ValidateCurrent();
            ValidateEntity(parent);
            ValidateEntity(child);
            var identity = (typeof(TParent), typeof(TChild), shape.Navigation);
            if (Collections.TryGetValue(identity, out var old) && !old.Equivalent(candidate))
                throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
            Register(parent);
            Register(child);
            Collections.TryAdd(identity, candidate);
        }
    }

    internal static IOrphanCollection Resolve(Type parent, Type child, string navigation)
    {
        lock (Sync)
        {
            if (navigation is null || !Collections.TryGetValue((parent, child, navigation), out var relation))
                throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
            relation.ValidateCurrent();
            return relation;
        }
    }

    private static void Register(IOrphanEntity binding)
    {
        ValidateEntity(binding);
        Entities.TryAdd(binding.EntityType, binding);
    }

    internal static bool CurrentForRead(Type type)
    {
        lock (Sync)
        {
            if (!Entities.TryGetValue(type, out var binding)) return false;
            try { binding.ValidateCurrent(); return true; }
            catch (OrphanException) { return false; }
        }
    }

    internal static IOrphanEntity GuardEntity(EntityDescriptor descriptor)
    {
        lock (Sync)
        {
            var matches = Entities.Values.Where(e =>
                OrphanDescriptorEquality.Same(e.Metadata.Copy(), descriptor)).Take(2).ToArray();
            if (matches.Length != 1) throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
            matches[0].ValidateCurrent();
            return matches[0];
        }
    }

    private static void ValidateEntity(IOrphanEntity binding)
    {
        binding.ValidateCurrent();
        if (Entities.TryGetValue(binding.EntityType, out var prior) && !prior.Equivalent(binding))
            throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
    }
}
