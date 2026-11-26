using System.Data.Common;

namespace Neemle.OrmLite.Tests.Graph;

internal sealed class SdetTrackedSource(string connectionString) : DbDataSource
{
    public override string ConnectionString => connectionString;
    internal List<SdetTrackedConnection> Connections { get; } = [];

    protected override DbConnection CreateDbConnection()
    {
        var connection = new SdetTrackedConnection(connectionString);
        Connections.Add(connection);
        return connection;
    }
}
