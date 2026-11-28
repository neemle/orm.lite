using System.Data.Common;

namespace Neemle.OrmLite.Tests.Unit.Joins;

internal sealed class FakeJoinSource(FakeJoinProbe probe) : DbDataSource
{
    public override string ConnectionString => "Host=fake-join";

    protected override DbConnection CreateDbConnection()
    {
        probe.Connections++;
        return new FakeJoinConnection(probe);
    }
}
