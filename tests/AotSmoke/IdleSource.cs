using System.Data.Common;

namespace AotSmoke;

internal sealed class IdleSource : DbDataSource
{
    public override string ConnectionString => "aot-smoke://idle";

    protected override DbConnection CreateDbConnection() =>
        throw new InvalidOperationException("the AOT smoke only renders; it never opens a connection.");
}
