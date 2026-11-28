using Neemle.OrmLite.Sql;

namespace Neemle.OrmLite.Postgres;

public sealed partial class PostgresProvider
{
    /// <summary>
    /// <c>REINDEX DATABASE</c>, <c>REINDEX TABLE &lt;table&gt;</c> or <c>REINDEX INDEX &lt;schema&gt;.&lt;index&gt;</c>, with
    /// <c>CONCURRENTLY</c> exactly when the mode is <see cref="ReindexMode.Concurrent"/>. The database form names no
    /// database (PostgreSQL 16 and later), so system catalogs are not rebuilt. The index is qualified by the schema of
    /// its table (from <see cref="RenderIndexSchemaLookup"/>), so search_path cannot resolve it to another table's index.
    /// </summary>
    public string RenderReindex(ReindexScope scope, ReindexMode mode, EntityDescriptor? table, IndexDescriptor? index, string? indexSchema)
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
            ReindexScope.Index when indexSchema is not null => sql.Ident(indexSchema).Dot().Ident(index!.Name).ToString(),
            ReindexScope.Index => sql.Ident(index!.Name).ToString(),
            _ => sql.ToString(),
        };
    }

    /// <summary>
    /// The schema of <paramref name="table"/>, resolved the way every other statement resolves the table's name:
    /// <c>SELECT n.nspname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid = '&lt;table&gt;'::regclass</c>.
    /// A PostgreSQL index always lives in its table's schema. A missing table raises 42P01.
    /// </summary>
    public string? RenderIndexSchemaLookup(EntityDescriptor table) =>
        new SqlWriter().Atom("SELECT").Bare("n.nspname").Atom("FROM").Bare("pg_class").Bare("c").Atom("JOIN").Bare("pg_namespace")
            .Bare("n").Atom("ON").Bare("n.oid").Atom("=").Bare("c.relnamespace").Atom("WHERE").Bare("c.oid").Atom("=")
            .Str(QuotedTable(table)).Attach("::regclass").ToString();
}
