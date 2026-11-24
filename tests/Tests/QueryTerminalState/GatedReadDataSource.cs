using System.Data.Common;

namespace Neemle.OrmLite.Tests.QueryTerminalState;

internal sealed class GatedReadDataSource(DbDataSource inner, Task gate, TaskCompletionSource reached) : DbDataSource
{
    public override string ConnectionString => inner.ConnectionString;

    protected override DbConnection CreateDbConnection() => inner.CreateConnection();

    protected override DbCommand CreateDbCommand(string? commandText = null) =>
        new GatedReadCommand(inner.CreateCommand(commandText), gate, reached);
}
