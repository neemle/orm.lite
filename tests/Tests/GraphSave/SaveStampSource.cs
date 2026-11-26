using System.Data.Common;

namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveStampSource(SaveFaultProbe probe) : DbDataSource
{
    public override string ConnectionString => "Host=independent-actor-stamp-fixture";
    protected override DbConnection CreateDbConnection()
    {
        probe.Opens++;
        return probe.Connection = new SaveFaultConnection(probe);
    }
}
