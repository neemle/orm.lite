using System;
using System.Data.Common;

namespace Neemle.Orm.Lite.Tests;

internal sealed class NeverUsedDataSource : DbDataSource
{
    private const string Message =
        "NeverUsedDataSource is a throwaway OrmContext.DataSource stand-in; every query in " +
        "Neemle.Orm.Lite.Tests uses On(sqliteConnection) and must never reach the data source itself.";

    public override string ConnectionString => throw new NotSupportedException(Message);

    protected override DbConnection CreateDbConnection() => throw new NotSupportedException(Message);
}
