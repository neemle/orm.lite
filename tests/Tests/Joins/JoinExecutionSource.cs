using System.Data.Common;
namespace Neemle.OrmLite.Tests.Joins;
internal sealed class JoinExecutionSource(JoinExecutionProbe probe) : DbDataSource
{
    internal JoinExecutionProbe Probe => probe;
    public override string ConnectionString => "Host=join-fixture";
    protected override DbConnection CreateDbConnection()
    {
        Interlocked.Increment(ref probe.Connections);
        return new JoinExecutionConnection(probe);
    }
}
