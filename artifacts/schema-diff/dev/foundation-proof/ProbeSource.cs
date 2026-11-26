using System.Data.Common;

namespace FoundationProof;

internal sealed class ProbeSource(Probe probe) : DbDataSource
{
    public override string ConnectionString => "fake-fixture";
    protected override DbConnection CreateDbConnection() => new ProbeConnection(probe);
}
