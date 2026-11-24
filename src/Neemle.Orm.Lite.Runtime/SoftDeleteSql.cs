#nullable enable

namespace Neemle.Orm.Lite;

    public static class SoftDeleteSql
    {
        public static string SetClause(ISqlDialect dialect, string column) =>
            dialect.Quote(column) + " = " + dialect.NowExpression;

        public static string LiveTerm(ISqlDialect dialect, string column) =>
            dialect.Quote(column) + " IS NULL";

        public static string LiveTerm(ISqlDialect dialect, string column, string qualifier) =>
            qualifier + "." + LiveTerm(dialect, column);
    }
