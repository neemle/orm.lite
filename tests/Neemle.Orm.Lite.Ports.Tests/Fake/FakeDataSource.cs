using System.Data.Common;

namespace Neemle.Orm.Lite.Ports.Tests.Fake;

public sealed class FakeDataSource(FakeDatabase store) : DbDataSource
{
    public override string ConnectionString => "fake";

    protected override DbConnection CreateDbConnection() => new FakeConnection(store);
}
