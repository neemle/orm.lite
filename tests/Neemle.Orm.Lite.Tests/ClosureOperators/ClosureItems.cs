using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sqlite;
using Xunit;

namespace Neemle.Orm.Lite.Tests.ClosureOperators;

public static class ClosureItems
{
    public const string Select = "SELECT * FROM \"closure_items\" WHERE ";

    static ClosureItems()
    {
        OrmMeta.Register<ClosureItem>(
            "closure_items",
            property => property.ToLowerInvariant(),
            [
                "id", "title", "rank", "level", "stars", "views",
                "score", "price", "active", "status", "review", "owner",
            ]);
    }

    public static TheoryData<string> Providers => ["postgres", "sqlite"];

    public static ISqlProvider Provider(string id) =>
        id == "postgres" ? PostgresProvider.Instance : SqliteProvider.Instance;

    public static Query<ClosureItem> Of(string provider)
    {
        var context = new OrmContext(new NeverUsedDataSource(), Provider(provider));
        return new Query<ClosureItem>(context, _ => throw new NotSupportedException());
    }
}
