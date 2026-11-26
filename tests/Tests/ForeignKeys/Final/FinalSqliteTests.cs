using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.ForeignKeys.Final;

public sealed class FinalSqliteTests
{
    [Theory(DisplayName = "[SEC][UC-003] enabled SQLite enforces only the explicitly opted-in pure-link endpoint")]
    [InlineData(LinkForeignKeyEnd.Source)]
    [InlineData(LinkForeignKeyEnd.Target)]
    public async Task Unannotated_link_endpoint_remains_unconstrained(LinkForeignKeyEnd end)
    {
        await using var database = await ForeignKeyDatabase.SqliteAsync();
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync("PRAGMA foreign_keys")));
        await FinalLinkWitness.VerifyAsync(database, end);
    }
}
