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
        var eligible = OneToOneReadGuard.Eligible(typeof(T), map);
        return reader =>
        {
            var entity = map(reader);
            GraphSaveOrigins.Observe(entity, map, hasMasks);
            OrphanReadOrigins.Observe(entity, map, hasMasks);
            if (eligible) OneToOneProvenance.MarkIfEligible(entity, map, hasMasks);
            return entity;
        };
    }
}
