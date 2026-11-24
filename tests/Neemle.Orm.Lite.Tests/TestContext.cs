using System.Data.Common;
using Neemle.Orm.Lite.Postgres;

namespace Neemle.Orm.Lite.Tests;

public static class TestContext
{
    public static OrmContext Of(DbDataSource source, ISqlDialect dialect) =>
        new(source, ProviderFor(dialect), dialect);

    public static ISqlProvider ProviderFor(ISqlDialect dialect) =>
        dialect is SqliteDialect ? FixtureProvider.Instance : PostgresProvider.Instance;
}
