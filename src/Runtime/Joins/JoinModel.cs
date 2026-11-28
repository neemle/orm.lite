namespace Neemle.OrmLite;

public static class JoinModel
{
    private static readonly object Gate = new();
    private static readonly Dictionary<Type, JoinEntityRegistration> Entities = [];

    public static void RegisterColumn<T, TValue>(EntityDescriptor descriptor, JoinColumnBinding<T, TValue> binding)
        where T : class
    {
        if (descriptor is null || binding is null || binding.Read is null)
            throw new JoinReadException(JoinError.InvalidRequest);
        try
        {
            lock (Gate)
            {
                if (!Entities.TryGetValue(typeof(T), out var entity))
                {
                    entity = new JoinEntityRegistration(new JoinEntityMetadata(typeof(T), descriptor));
                }
                entity.Metadata.Descriptor(descriptor);
                var shape = entity.Metadata.Property(binding.Property);
                JoinScalarTypes.Binding(typeof(TValue), shape.Column);
                entity.Add(new JoinRegisteredColumn<TValue>(binding.Property, shape.Column,
                    shape.Calculated, binding.Read));
                Entities.TryAdd(typeof(T), entity);
            }
        }
        catch (JoinReadException) { throw; }
        catch (Exception error) { throw new JoinReadException(JoinError.UnsupportedSource, error); }
    }

    internal static JoinEntityRegistration Find(Type type)
    {
        lock (Gate) return Entities.TryGetValue(type, out var entity) ? entity
            : throw new JoinReadException(JoinError.UnknownSource);
    }
}
