namespace Neemle.OrmLite.Sqlite;

internal static class SqliteOrphanDataLiterals
{
    internal const string CatalogAcquire = "PRAGMA foreign_keys";

    internal const string CatalogColumns =
        "SELECT name,type,\"notnull\",pk,hidden,dflt_value "
        + "FROM pragma_table_xinfo(@owner,'main') LIMIT 257";

    internal const string CatalogObjects =
        "SELECT type,name,tbl_name,sql FROM main.sqlite_schema "
        + "WHERE tbl_name=@owner AND sql IS NOT NULL ORDER BY type,name LIMIT 257";

    internal const string CatalogRefuseTemp =
        "SELECT 1 FROM temp.sqlite_schema "
        + "WHERE tbl_name=@owner LIMIT 1";

    internal const string CatalogIncoming =
        "SELECT f.on_delete,f.on_update FROM main.sqlite_schema m "
        + "JOIN pragma_foreign_key_list(m.name,'main') f WHERE m.type='table' AND f.\"table\"=@owner LIMIT 257";

    internal const string CatalogSafe = "NO ACTION";

    internal const string DefaultsEmptyText = "''";

    internal const string DefaultsEmptyJson = "'{}'";

    internal const string DefinitionsUniqueIndex = "CREATE UNIQUE INDEX ";

    internal const string DefinitionsIndex = "CREATE INDEX ";

    internal const string DefinitionsIfNotExists = "IF NOT EXISTS ";

    internal const string HistoryIndex =
        "SELECT cid,coll,(SELECT partial FROM pragma_index_list('row_history','main') "
        + "WHERE name=@owner) FROM ";

    internal const string HistoryIndex2 = "pragma_index_xinfo(@owner,'main') WHERE key=1 LIMIT 257";
}
