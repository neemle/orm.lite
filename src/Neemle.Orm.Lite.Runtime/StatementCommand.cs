#nullable enable
using System.Data.Common;

namespace Neemle.Orm.Lite;

    internal static class StatementCommand
    {
        public static DbCommand Create(DbDataSource ds, DbConnection? conn, DbTransaction? tx, string sql)
        {
            if (conn is null) return ds.CreateCommand(sql);
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = tx;
            return cmd;
        }
    }
