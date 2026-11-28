using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Sqlite;

public sealed partial class SqliteProvider
{
    /// <summary>
    /// <c>REINDEX</c>, <c>REINDEX main.&lt;table&gt;</c> or <c>REINDEX main.&lt;index&gt;</c>, blocking only; Concurrent is
    /// refused by capability first. Index names are unique per SQLite schema, so there is no schema lookup and
    /// <paramref name="indexSchema"/> is not used.
    /// </summary>
    public string RenderReindex(ReindexScope scope, ReindexMode mode, EntityDescriptor? table, IndexDescriptor? index, string? indexSchema)
    {
        if (mode != ReindexMode.Blocking)
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "SQLite rebuilds indexes only in Blocking mode.");
        return scope switch
        {
            ReindexScope.Database => new SqlWriter().Atom("REINDEX").ToString(),
            ReindexScope.Table when table is not null =>
                new SqlWriter().Atom("REINDEX").Atom(MainSchema).Dot().Attach(QuotedTable(table)).ToString(),
            ReindexScope.Index when index is not null =>
                new SqlWriter().Atom("REINDEX").Atom(MainSchema).Dot().Ident(index.Name).ToString(),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "not a REINDEX scope with its target."),
        };
    }
}
