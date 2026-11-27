using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class OrphanGuardColumns
{
    internal static void Validate(ISqlProvider provider, EntityDescriptor entity,
        IReadOnlyList<OrphanGuardColumn> columns)
    {
        if (columns.Count != entity.Columns.Count) throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
        foreach (var declared in entity.Columns)
        {
            var actual = columns.SingleOrDefault(c => c.Name == declared.Column);
            if (actual is null || actual.Nullable != declared.Nullable || actual.Key != declared.Key)
                throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
            var type = provider.CreateSerializer().Fragment(
                new TypeRef(declared.Type, declared.Precision, declared.Scale));
            if (Type(type) != Type(actual.Type)) throw OrphanRefusal.At(OrphanError.UnsupportedMapping);
            if (actual.Computed) throw OrphanRefusal.At(OrphanError.UnverifiedEffects);
        }
    }

    private static string Type(string type) => string.Concat(type.Where(c => !char.IsWhiteSpace(c)))
        .ToLowerInvariant();
}
