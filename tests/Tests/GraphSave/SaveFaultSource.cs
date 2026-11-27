using System.Data.Common;

namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveFaultSource(SaveFaultProbe probe) : DbDataSource
{
    public override string ConnectionString => "Host=independent-fault-fixture";
    protected override DbConnection CreateDbConnection() => new SaveFaultConnection(probe);
    protected override async ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken ct = default)
    {
        probe.Opens++;
        var connection = new SaveFaultConnection(probe);
        probe.Connection = connection;
        await connection.OpenAsync(ct);
        return connection;
    }
}
