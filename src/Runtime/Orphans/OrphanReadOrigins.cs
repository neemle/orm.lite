using System.Runtime.CompilerServices;

namespace Neemle.OrmLite;

internal static class OrphanReadOrigins
{
    private static readonly ConditionalWeakTable<object, object> Partial = new();
    private static readonly object Tag = new();

    internal static void MarkPartial(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Partial.GetValue(entity, _ => Tag);
    }

    internal static void RefuseKnownPartial(object entity)
    {
        if (Partial.TryGetValue(entity, out _)) throw OrphanRefusal.At(OrphanError.UnsupportedVisibility);
    }
}
