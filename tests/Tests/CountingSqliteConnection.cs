using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Neemle.OrmLite.Tests;

internal sealed class CountingSqliteConnection(string connectionString, StatementRecorder recorder)
    : SqliteConnection(connectionString)
{
    private readonly int _id = recorder.NextConnectionId();

    protected override DbCommand CreateDbCommand()
    {
        var command = new CountingSqliteCommand(recorder, _id);
        command.Connection = this;
        return command;
    }
}
