using System.Data.Common;

namespace Neemle.Orm.Lite.Tests;

public static class TestContext
{
    public static OrmContext Of(DbDataSource source, ISqlProvider provider) => new(source, provider);
}
