using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Tests.Nodes;
using Npgsql;
using Xunit;

namespace Neemle.OrmLite.Tests;

public sealed class CalculatedPostgresTests
{
    [PostgresFact(DisplayName = "[UC-001] PostgreSQL computes Lower across generated reads")]
    public async Task Postgres_computes_full_entity_and_relation_reads()
    {
        var server = Environment.GetEnvironmentVariable(PostgresFactAttribute.Variable)!;
        await using var admin = NpgsqlDataSource.Create(server);
        var name = "orm_calculated_" + Guid.NewGuid().ToString("N");
        await using (var create = admin.CreateCommand("CREATE DATABASE \"" + name + "\""))
            await create.ExecuteNonQueryAsync();
        try
        {
            var settings = new NpgsqlConnectionStringBuilder(server) { Database = name };
            await using var source = NpgsqlDataSource.Create(settings.ConnectionString);
            await using var db = new OrmContext(source, PostgresProvider.Instance);
            var model = CalculatedFieldTests.Model;
            model.RunModuleInitializer();
            await MigrationRunner.RunAsync(source, db.Provider, model.Contributions);
            await CalculatedRoundTrip.VerifyAsync(db);
            await using var columns = source.CreateCommand(
                "SELECT count(*) FROM information_schema.columns "
                + "WHERE table_name = 'calculated_notes' AND column_name = 'lower_label'");
            Assert.Equal(0L, await columns.ExecuteScalarAsync());
        }
        finally
        {
            await using var drop = admin.CreateCommand("DROP DATABASE \"" + name + "\" WITH (FORCE)");
            await drop.ExecuteNonQueryAsync();
        }
    }
}
