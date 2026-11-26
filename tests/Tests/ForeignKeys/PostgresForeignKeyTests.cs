using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Tests.Nodes;

namespace Neemle.OrmLite.Tests.ForeignKeys;

public sealed class PostgresForeignKeyTests
{
    [PostgresFact(DisplayName = "[UC-003] PostgreSQL catalogs and enforces all four fresh delete policies and reruns")]
    public async Task Actions_have_real_catalog_orphan_delete_and_history_witnesses()
    {
        foreach (var action in Enum.GetValues<ReferentialAction>())
        {
            await using var database = await ForeignKeyDatabase.PostgresAsync();
            await ForeignKeyEnforcement.VerifyAsync(database, action);
        }
    }

    [PostgresFact(DisplayName = "[SEC][UC-003] quoted PostgreSQL constraint names remain exact catalog data")]
    public async Task Hostile_quoted_name_cannot_execute_statements()
    {
        await using var database = await ForeignKeyDatabase.PostgresAsync();
        await ForeignKeyEnforcement.QuotedNameAsync(database);
    }
    [PostgresFact(DisplayName = "[UC-003] generated pure links enforce independent Restrict and Cascade endpoints")]
    public async Task Pure_link_has_real_endpoint_integrity_and_actions()
    {
        await using var database = await ForeignKeyDatabase.PostgresAsync();
        await ForeignKeyLinks.VerifyAsync(database);
    }
}
