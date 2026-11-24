using System.Data.Common;

namespace Neemle.Orm.Lite.Tests;

// ORML-76: the one data source of every rendering test that must never reach a database. CreateDbConnection throws and
// counts the attempt, so a test can also assert that nothing touched it (Touches).
internal sealed class UnreachableDataSource : DbDataSource
{
    public int Touches { get; private set; }

    public override string ConnectionString => "fake://unreachable";

    protected override DbConnection CreateDbConnection()
    {
        Touches++;
        throw new InvalidOperationException("this fixture must not open a connection.");
    }
}
