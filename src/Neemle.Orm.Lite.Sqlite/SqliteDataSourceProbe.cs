using System.Data.Common;

namespace Neemle.Orm.Lite.Sqlite;

/// <summary>
/// Refuses a data source whose connections are not the library's own. The provider casts every connection it
/// stamps or begins a transaction on to <see cref="OrmSqliteConnection"/>, and only that connection carries the
/// orm_* functions, the orm_decimal collation and the engine version guard.
/// </summary>
internal static class SqliteDataSourceProbe
{
    internal static void Require(string name, DbDataSource source)
    {
        using var probe = source.CreateConnection();
        if (probe is OrmSqliteConnection) return;
        throw new InvalidOperationException(
            $"the sqlite data source for the {name} lane creates {probe.GetType().FullName} connections, "
            + $"and provider 'sqlite' needs the {nameof(OrmSqliteConnection)} connections of "
            + "SqliteProviderFactory.CreateDataSource: a plain connection lacks the orm_* functions, "
            + "the orm_decimal collation and the SQLite version guard. Create the source with the factory "
            + "(or through OrmLite:ConnectionString and OrmLite.OpenAsync) instead of passing your own.");
    }
}
