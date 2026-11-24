#nullable enable
using Neemle.Orm.Lite.Nodes;

namespace Neemle.Orm.Lite;

    public static class SoftDeleteSql
    {
        public static string SetClause(ISqlProvider provider, string column) =>
            Text(provider, new ColumnRef(column)) + " = " + Text(provider, new ClockNow());

        public static string LiveTerm(ISqlProvider provider, string column) =>
            Text(provider, new ColumnRef(column)) + " IS NULL";

        public static string LiveTerm(ISqlProvider provider, string column, string qualifier) =>
            qualifier + "." + LiveTerm(provider, column);

        private static string Text(ISqlProvider provider, Node node) => provider.CreateSerializer().Fragment(node);
    }
