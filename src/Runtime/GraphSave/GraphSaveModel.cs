namespace Neemle.OrmLite;

public static class GraphSaveModel
{
    private static readonly object Sync = new();
    private static readonly Dictionary<Type, IGraphSaveEntity> Bindings = [];

    public static void Register<T, TKey>(GraphSaveEntityBinding<T, TKey> binding)
        where T : class where TKey : notnull
    {
        if (binding is null) throw GraphSaveRefusal.At(GraphSaveError.InvalidRequest);
        var candidate = (IGraphSaveEntity)binding;
        lock (Sync)
        {
            ValidateCurrent(candidate);
            if (Bindings.TryGetValue(typeof(T), out var prior))
            {
                if (!prior.Equivalent(candidate)) throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
                return;
            }
            if (Bindings.Values.Any(b => b.Descriptor.Table == candidate.Descriptor.Table
                && b.EntityType.AssemblyQualifiedName == candidate.EntityType.AssemblyQualifiedName))
                throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
            Bindings.Add(typeof(T), candidate);
        }
    }

    internal static IGraphSaveEntity Resolve(Type type)
    {
        lock (Sync)
        {
            if (!Bindings.TryGetValue(type, out var binding))
                throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
            ValidateCurrent(binding);
            return binding;
        }
    }

    internal static IGraphSaveRelation Edge(Type source, Type target, string navigation)
    {
        var binding = Resolve(source);
        return binding.Relations.SingleOrDefault(r => r.TargetType == target && r.Shape.Navigation == navigation)
            ?? throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
    }

    internal static bool Eligible(Type type, Delegate mapper)
    {
        lock (Sync)
            return Bindings.TryGetValue(type, out var binding) && binding.Mapper.Equals(mapper)
                && ReferenceEquals(OrmMeta.ReadDescriptor(type), binding.Original)
                && GraphSaveDescriptors.Same(binding.Descriptor, binding.Original);
    }

    private static void ValidateCurrent(IGraphSaveEntity binding)
    {
        if (binding.HasReadPolicy || !ReferenceEquals(OrmMeta.ReadDescriptor(binding.EntityType), binding.Original)
            || !GraphSaveDescriptors.Same(binding.Descriptor, binding.Original))
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
    }
}
