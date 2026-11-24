using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;

namespace Neemle.OrmLite.Tests.ClauseRefusal;

internal static class ClauseWorld
{
    private static readonly object Gate = new();

    private static bool _registered;

    internal static string Column(string property) =>
        property == nameof(ClauseSoft.DeletedAt) ? "deleted_at" : property.ToLowerInvariant();

    internal static OrmContext Context(string provider) => Context(ProviderOf(provider));

    internal static OrmContext Context(ISqlProvider provider)
    {
        Register();
        return new OrmContext(new NeverUsedDataSource(), provider);
    }

    internal static ISqlProvider ProviderOf(string provider) =>
        provider == "postgres" ? PostgresProvider.Instance : SqliteProvider.Instance;

    private static void Register()
    {
        lock (Gate)
        {
            if (_registered) return;
            string[] columns = ["id", "name", "rank", "price"];
            OrmMeta.Register<ClausePlain>("clause_plain", Column, [.. columns, "doc"]);
            OrmMeta.Register<ClauseSoft>(
                "clause_soft",
                Column,
                [.. columns, "deleted_at", "doc"],
                new SoftDeleteMeta("DeletedAt", KeyColumn: "id"));
            OrmMeta.Register<ClauseView>(
                EntityDescriptor.Of(nameof(ClauseView), "clause_view", false)
                    .Property("Id", "id", ColumnType.Uuid, false, true)
                    .Property("Name", "name", ColumnType.Text, false, false)
                    .Property("Rank", "rank", ColumnType.Int32, false, false)
                    .View());
            _registered = true;
        }
    }
}
