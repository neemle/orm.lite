#nullable enable

namespace Neemle.Orm.Lite;

    public static class SoftDeleteSql
    {
        private static readonly PgDialect Pg = new PgDialect();

        public static string SetClause(ISqlDialect dialect, string column) =>
            dialect.Quote(column) + " = " + dialect.NowExpression;

        public static string LiveTerm(ISqlDialect dialect, string column) =>
            dialect.Quote(column) + " IS NULL";

        public static string LiveTerm(ISqlDialect dialect, string column, string qualifier) =>
            qualifier + "." + LiveTerm(dialect, column);

        public static string DeleteByKey(string table, string keyColumn, string softColumn) =>
            "UPDATE " + Pg.Quote(table) + " SET " + SetClause(Pg, softColumn)
            + " WHERE " + Pg.Quote(keyColumn) + " = @k AND " + LiveTerm(Pg, softColumn);
    }
