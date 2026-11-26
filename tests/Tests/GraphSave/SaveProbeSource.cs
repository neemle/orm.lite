using System.Data.Common;

namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveProbeSource : DbDataSource
{
    internal readonly Exception Stop = new InvalidOperationException("independent preflight admission witness");
    internal int Opens { get; private set; }
    public override string ConnectionString => "Host=preflight-witness";
    protected override DbConnection CreateDbConnection() => throw new NotSupportedException();
    protected override ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken ct = default)
    {
        Opens++;
        return ValueTask.FromException<DbConnection>(Stop);
    }
}
