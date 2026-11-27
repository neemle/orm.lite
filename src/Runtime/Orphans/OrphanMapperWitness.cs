namespace Neemle.OrmLite;

internal static class OrphanMapperWitness
{
    internal static bool Full(Type type, Delegate mapper) =>
        Current(type) is { } binding && binding.Maps(mapper);

    internal static bool Mapped(Type type, IGraphEntityBinding binding) =>
        type == binding.EntityType && ReferenceEquals(Current(type), binding);

    private static IGraphEntityBinding? Current(Type type)
    {
        if (!OrphanModel.CurrentForRead(type)) return null;
        try
        {
            var binding = GraphModel.Find(type);
            return ReferenceEquals(binding.Descriptor, OrmMeta.ReadDescriptor(type)) ? binding : null;
        }
        catch (GraphRequestException) { return null; }
    }
}
