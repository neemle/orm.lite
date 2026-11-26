using System.Data.Common;

namespace FaultProof;

internal sealed class FaultSource(FaultControl control) : DbDataSource
{
    public override string ConnectionString => "fake-isolated-fixture";
    protected override DbConnection CreateDbConnection() => new FaultConnection(control);
}
