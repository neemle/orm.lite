using System.Data.Common;

namespace PolicySmoke;

internal sealed class ObservedSource(DbDataSource inner) : DbDataSource
{
    internal int Opens { get; private set; }
    public override string ConnectionString => inner.ConnectionString;
    protected override DbConnection CreateDbConnection() => inner.CreateConnection();
    protected override async ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken ct = default)
    {
        Opens++;
        return await inner.OpenConnectionAsync(ct);
    }
}
