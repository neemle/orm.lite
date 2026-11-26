using System.Data.Common;

namespace Neemle.OrmLite;

internal static class OneToOneReadMap
{
    internal static Func<DbDataReader, T> Wrap<T>(Func<DbDataReader, T> map, bool hasMasks)
    {
        if (!OneToOneReadGuard.Eligible(typeof(T), map)) return map;
        return reader =>
        {
            var entity = map(reader);
            OneToOneProvenance.MarkIfEligible(entity, map, hasMasks);
            return entity;
        };
    }
}
