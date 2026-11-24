using System.Linq;
using Microsoft.Data.Sqlite;

namespace Neemle.OrmLite.Tests;

internal sealed class CountingSqliteCommand(StatementRecorder recorder, int connectionId) : SqliteCommand
{
    public override int ExecuteNonQuery()
    {
        recorder.Add(new StatementRecorder.Entry(
            CommandText,
            Parameters.Cast<SqliteParameter>().Select(p => p.Value).ToArray(),
            connectionId,
            Transaction is not null));
        return base.ExecuteNonQuery();
    }
}
