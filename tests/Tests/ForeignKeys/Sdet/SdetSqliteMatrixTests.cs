using Xunit;

namespace Neemle.OrmLite.Tests.ForeignKeys.Sdet;

public sealed class SdetSqliteMatrixTests
{
    [Fact(DisplayName = "[UC-003] enabled SQLite accepts valid and nullable FK updates with exact final references")]
    public async Task Valid_and_nullable_updates_succeed()
    {
        await using var database = await ForeignKeyDatabase.SqliteAsync();
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync("PRAGMA foreign_keys")));
        await SdetUpdateWitness.VerifyAsync(database);
    }

    [Fact(DisplayName = "[SEC][UC-003] SQLite soft delete and generated writes never infer FK graph cascades")]
    public async Task Soft_delete_and_generated_writes_preserve_their_existing_meaning()
    {
        await using var database = await ForeignKeyDatabase.SqliteAsync();
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync("PRAGMA foreign_keys")));
        await SdetSoftComposition.VerifyAsync(database);
    }

    [Fact(DisplayName = "[SEC][UC-003] SQLite hostile table and column names retain exact enforced endpoints")]
    public async Task Quoted_table_and_column_names_cannot_execute_payloads()
    {
        await using var database = await ForeignKeyDatabase.SqliteAsync();
        Assert.Equal(1L, Convert.ToInt64(await database.ScalarAsync("PRAGMA foreign_keys")));
        await SdetHostileIdentifiers.VerifyAsync(database);
    }
}
