namespace Neemle.OrmLite.Sqlite;

internal static class SqliteSchemaDataLiterals
{
    internal const string Databases =
        "SELECT name,file FROM pragma_database_list";

    internal const string TempShadows =
        "SELECT name FROM temp.sqlite_schema";

    internal const string ColumnSelect =
        "SELECT name,type,\"notnull\",dflt_value,pk,hidden ";

    internal const string ColumnPragma =
        "FROM pragma_table_xinfo(@table,'main')";

    internal const string Schema =
        "SELECT type,name,tbl_name,sql FROM main.sqlite_schema";

    internal const string HistoryVersions =
        "SELECT version FROM main.schema_migrations";

    internal const string IndexSelect =
        "SELECT name,\"unique\",origin,partial ";

    internal const string IndexPragma =
        "FROM pragma_index_list(@table,'main')";

    internal const string IndexTermSelect =
        "SELECT seqno,cid,name,desc,coll,\"key\" ";

    internal const string IndexTermPragma =
        "FROM pragma_index_xinfo(@index,'main')";

    internal const string ForeignKeys =
        "SELECT * FROM pragma_foreign_key_list(@table,'main')";

    internal const string ForeignKeysOff =
        "PRAGMA foreign_keys=OFF";

    internal const string ForeignKeysOn =
        "PRAGMA foreign_keys=ON";

    internal const string ForeignKeysValue =
        "PRAGMA foreign_keys";

    internal const string EmptyTextDefault =
        "''";

    internal const string ClockDefault =
        "strftime('%Y-%m-%dT%H:%M:%f', 'now') || '0000Z'";

    internal const string ParenthesizedClockDefault =
        "(strftime('%Y-%m-%dT%H:%M:%f', 'now') || '0000Z')";

}
