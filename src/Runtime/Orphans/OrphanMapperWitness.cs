namespace Neemle.OrmLite;

internal static class OrphanMapperWitness
{
    internal static bool Full(Type type, Delegate mapper)
    {
        if (!OrphanModel.CurrentForRead(type)) return false;
        try
        {
            var binding = GraphModel.Find(type);
            return ReferenceEquals(binding.Descriptor, OrmMeta.ReadDescriptor(type)) && binding.Maps(mapper);
        }
        catch (GraphRequestException) { return false; }
    }

    internal static bool Mapped(Type type, IGraphEntityBinding binding)
    {
        if (!OrphanModel.CurrentForRead(type) || type != binding.EntityType) return false;
        try
        {
            return ReferenceEquals(GraphModel.Find(type), binding)
                && ReferenceEquals(binding.Descriptor, OrmMeta.ReadDescriptor(type));
        }
        catch (GraphRequestException) { return false; }
    }
}
