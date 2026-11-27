using System.Runtime.CompilerServices;
using System.Data.Common;

namespace Neemle.OrmLite;

internal static class GraphSaveOrigins
{
    private static readonly ConditionalWeakTable<object, GraphSaveOriginTag> Tags = new();

    internal static void Observe(object? entity, Delegate mapper, bool mayBePartial)
    {
        if (entity is null || entity is string || entity.GetType().IsValueType) return;
        if (mayBePartial) { Tags.GetValue(entity, _ => new(true)).MarkPartial(); return; }
        if (GraphSaveModel.Eligible(entity.GetType(), mapper)) Tags.GetValue(entity, _ => new(false));
    }

    internal static void RefuseKnownPartial(object entity)
    {
        if (Tags.TryGetValue(entity, out var tag) && tag.Partial)
            throw GraphSaveRefusal.At(GraphSaveError.UnsupportedMapping);
    }

    internal static Func<DbDataReader, T> Wrap<T>(Func<DbDataReader, T> mapper, bool partial) => reader =>
    {
        var entity = mapper(reader);
        Observe(entity, mapper, partial);
        return entity;
    };
}
