using System.Runtime.CompilerServices;
using System.Data.Common;

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

    internal static void Observe(object? entity, Delegate mapper, bool partial)
    {
        if (entity is null or string || entity.GetType().IsValueType) return;
        if (partial || !OrphanMapperWitness.Full(entity.GetType(), mapper)) MarkPartial(entity);
    }

    internal static void ObserveMapped(object entity, IGraphEntityBinding binding, bool partial)
    {
        if (partial || !OrphanMapperWitness.Mapped(entity.GetType(), binding)) MarkPartial(entity);
    }

    internal static Func<DbDataReader, T> Wrap<T>(Func<DbDataReader, T> map, bool partial) => reader =>
    {
        var entity = map(reader);
        Observe(entity, map, partial);
        return entity;
    };
}
