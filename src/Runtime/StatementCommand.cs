#nullable enable
using System.Data.Common;

namespace Neemle.OrmLite;

    internal static class StatementCommand
    {
        public static DbCommand Create(DbDataSource ds, DbConnection? conn, DbTransaction? tx, Statement statement)
        {
            var sql = statement.Text;
            if (conn is null)
            {
                var gated = ds.CreateCommand(sql);
                if (gated is GatedDataSource.GatedCommand command) command.Kind = statement.Kind;
                return gated;
            }
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = tx;
            return cmd;
        }
    }
