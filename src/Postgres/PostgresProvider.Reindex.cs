using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Postgres;

public sealed partial class PostgresProvider
{
    /// <summary>
    /// <c>REINDEX DATABASE</c>, <c>REINDEX TABLE &lt;table&gt;</c> or <c>REINDEX INDEX &lt;index&gt;</c>, with
    /// <c>CONCURRENTLY</c> exactly when the mode is <see cref="ReindexMode.Concurrent"/>. The database form names no
    /// database (PostgreSQL 16 and later), so system catalogs are not rebuilt.
    /// </summary>
    public string RenderReindex(ReindexScope scope, ReindexMode mode, EntityDescriptor? table, IndexDescriptor? index)
    {
        var sql = new SqlWriter().Atom("REINDEX");
        sql = scope switch
        {
            ReindexScope.Database => sql.Atom("DATABASE"),
            ReindexScope.Table when table is not null => sql.Atom("TABLE"),
            ReindexScope.Index when index is not null => sql.Atom("INDEX"),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "not a REINDEX scope with its target."),
        };
        sql = mode switch
        {
            ReindexMode.Blocking => sql,
            ReindexMode.Concurrent => sql.Atom("CONCURRENTLY"),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "not a REINDEX mode."),
        };
        return scope switch
        {
            ReindexScope.Table => sql.Atom(QuotedTable(table!)).ToString(),
            ReindexScope.Index => sql.Ident(index!.Name).ToString(),
            _ => sql.ToString(),
        };
    }
}
