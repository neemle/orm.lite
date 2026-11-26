using Neemle.OrmLite.Tests.Nodes;

namespace Neemle.OrmLite.Tests.ForeignKeys.Sdet;

public sealed class SdetPostgresMatrixTests
{
    [PostgresFact(DisplayName = "[UC-003] PostgreSQL accepts valid and nullable FK updates with exact references")]
    public async Task Valid_and_nullable_updates_succeed()
    {
        await using var database = await ForeignKeyDatabase.PostgresAsync();
        await SdetUpdateWitness.VerifyAsync(database);
    }

    [PostgresFact(DisplayName = "[SEC][UC-003] PostgreSQL soft delete and generated writes infer no graph cascade")]
    public async Task Soft_delete_and_generated_writes_preserve_their_existing_meaning()
    {
        await using var database = await ForeignKeyDatabase.PostgresAsync();
        await SdetSoftComposition.VerifyAsync(database);
    }

    [PostgresFact(DisplayName = "[UC-003] PostgreSQL existing baseline retains exact history and orphan data")]
    public async Task Existing_baseline_is_not_implicitly_retrofitted()
    {
        await using var database = await ForeignKeyDatabase.PostgresAsync();
        await SdetExistingBaseline.VerifyAsync(database);
    }

    [PostgresFact(DisplayName = "[SEC][UC-003] PostgreSQL hostile table and column names retain exact endpoints")]
    public async Task Quoted_table_and_column_names_cannot_execute_payloads()
    {
        await using var database = await ForeignKeyDatabase.PostgresAsync();
        await SdetHostileIdentifiers.VerifyAsync(database);
    }
}
