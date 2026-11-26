using System.Runtime.CompilerServices;

namespace Neemle.OrmLite;

internal static class OneToOneProvenance
{
    private static readonly ConditionalWeakTable<object, OneToOneVisibilityTag> Tags = new();

    internal static void MarkIfEligible(object? entity, Delegate map, bool hasMasks)
    {
        if (entity is null || !OneToOneReadGuard.Eligible(entity.GetType(), map)) return;
        Tags.AddOrUpdate(entity, new OneToOneVisibilityTag(hasMasks));
    }

    internal static void Require(object entity, string navigation)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!Tags.TryGetValue(entity, out var tag))
            throw new OneToOneDeclarationException(OneToOneError.UnknownProvenance,
                entity.GetType().FullName, navigation);
        if (tag.HasMasks)
            throw new OneToOneDeclarationException(OneToOneError.UnsupportedVisibility,
                entity.GetType().FullName, navigation);
    }
}
