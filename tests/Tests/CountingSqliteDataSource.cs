using System.Data.Common;

namespace Neemle.OrmLite.Tests;

internal sealed class CountingSqliteDataSource(string path, StatementRecorder recorder) : DbDataSource
{
    public override string ConnectionString => "Data Source=" + path + ";Pooling=False";

    protected override DbConnection CreateDbConnection() => new CountingSqliteConnection(ConnectionString, recorder);
}
