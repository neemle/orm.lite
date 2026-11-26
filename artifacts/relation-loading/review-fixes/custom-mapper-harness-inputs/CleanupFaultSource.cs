using System.Data.Common;

namespace PolicySmoke;

internal sealed class CleanupFaultSource(DbDataSource inner, CleanupFaultPlan plan) : DbDataSource
{
    public override string ConnectionString => inner.ConnectionString;
    protected override DbConnection CreateDbConnection() => throw new NotSupportedException();
    protected override async ValueTask<DbConnection> OpenDbConnectionAsync(CancellationToken ct = default) =>
        new CleanupFaultConnection(await inner.OpenConnectionAsync(ct), plan);
}
