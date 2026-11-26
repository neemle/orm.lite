using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.ForeignKeys.Final;

public sealed class FinalPostgresTests
{
    [Theory(DisplayName = "[SEC][UC-003] PostgreSQL enforces only the explicitly opted-in pure-link endpoint")]
    [InlineData(LinkForeignKeyEnd.Source)]
    [InlineData(LinkForeignKeyEnd.Target)]
    public async Task Unannotated_link_endpoint_remains_unconstrained(LinkForeignKeyEnd end)
    {
        await using var database = await ForeignKeyDatabase.PostgresAsync();
        await FinalLinkWitness.VerifyAsync(database, end);
    }
}
