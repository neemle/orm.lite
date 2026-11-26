using System.Data.Common;

namespace Neemle.OrmLite.Tests.Graph.Security;

internal sealed class SecuritySource(DbDataSource inner) : DbDataSource
{
    public override string ConnectionString => inner.ConnectionString;
    internal List<SecurityConnection> Connections { get; } = [];
    internal List<SecurityCommand> Commands { get; } = [];
    internal List<SecurityCommandRecord> Reads { get; } = [];
    internal List<DbDataReader> Readers { get; } = [];
    internal bool FailRelated { get; set; }
    internal TimeoutException Timeout { get; } = new("Injected awaited graph command deadline elapsed.");

    protected override DbConnection CreateDbConnection()
    {
        var connection = new SecurityConnection(inner.CreateConnection(), this);
        Connections.Add(connection);
        return connection;
    }

    internal void Capture(DbCommand command)
    {
        Reads.Add(new SecurityCommandRecord(command.CommandText,
            command.Parameters.Cast<DbParameter>().Select(p => p.Value).ToArray(), command.Transaction));
    }
}
