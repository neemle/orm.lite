using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

/// <summary>Compares a table's catalog columns with the entity mapping the replacement writes through.</summary>
public static class OrphanGuardColumns
{
    /// <summary>
    /// Refuses unless the table has exactly the mapped columns, each with the mapped nullability, key membership and
    /// type, and none of them computed.
    /// </summary>
    /// <param name="provider">The provider whose type spelling the catalog uses.</param>
    /// <param name="entity">The mapped entity.</param>
    /// <param name="columns">The columns the catalog reported.</param>
    /// <exception cref="OrphanException"><see cref="OrphanError.UnsupportedMapping"/> on a mismatch,
    /// <see cref="OrphanError.UnverifiedEffects"/> for a computed column.</exception>
    public static void Validate(ISqlProvider provider, EntityDescriptor entity,
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

    private static string Type(string type) => type.Trim(' ').Replace(", ", ",", StringComparison.Ordinal)
        .ToLowerInvariant();
}
