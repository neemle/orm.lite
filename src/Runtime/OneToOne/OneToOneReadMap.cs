using System.Data.Common;

namespace Neemle.OrmLite;

internal static class OneToOneReadMap
{
    internal static async Task<T?> FirstAsync<T>(StatementExecutor executor, Statement statement,
        Func<DbDataReader, T> map, bool hasMasks)
    {
        var rows = await executor.RowsAsync(statement, Wrap(map, hasMasks));
        return rows.Count > 0 ? rows[0] : default;
    }

    internal static Func<DbDataReader, T> Wrap<T>(Func<DbDataReader, T> map, bool hasMasks)
    {
        var read = OrphanReadOrigins.Wrap(map, hasMasks);
        if (!OneToOneReadGuard.Eligible(typeof(T), map)) return read;
        return reader =>
        {
            var entity = read(reader);
            OneToOneProvenance.MarkIfEligible(entity, map, hasMasks);
            return entity;
        };
    }
}
