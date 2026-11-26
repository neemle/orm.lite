using Neemle.OrmLite.Postgres;
using Npgsql;
using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphCalculatedIntegrationTests
{
    [Fact(DisplayName = "[AC-108-01/03] generated nullable string/value inverse keys and calculated graph fields work")]
    public async Task Sqlite_generated_graph_preserves_nullable_membership_and_calculations()
    {
        await using var db = await GraphDatabase.OpenAsync();
        await SdetGraphGeneratedConsumer.SeedAsync(db.Context);
        await SdetGraphGeneratedConsumer.AssertReadAsync(db.Context);
        var masked = await SdetGraphGeneratedConsumer.ReadAsync(db.Context, "Masked");
        Assert.Equal("mixed", masked[0]);
        Assert.All(masked.Skip(1), value => Assert.True(Assert.IsType<bool>(value)));
        db.Statements.Clear();
        await Assert.ThrowsAsync<NotSupportedException>(() => SdetGraphGeneratedConsumer.UnsafeAsync(db.Context));
        Assert.Empty(db.Statements);
    }

    [PostgresFact(DisplayName = "[AC-108-06] generated nullable inverse graph and Lower have PostgreSQL parity")]
    public async Task PostgreSql_generated_graph_has_equivalent_membership_and_calculated_values()
    {
        var server = Environment.GetEnvironmentVariable(PostgresFactAttribute.Variable)!;
        await using var admin = NpgsqlDataSource.Create(server);
        var name = "orm_sdet_graph_" + Guid.NewGuid().ToString("N");
        await using (var create = admin.CreateCommand("CREATE DATABASE \"" + name + "\""))
            await create.ExecuteNonQueryAsync();
        try
        {
            await using var source = NpgsqlDataSource.Create(
                new NpgsqlConnectionStringBuilder(server) { Database = name }.ConnectionString);
            await using var db = new OrmContext(source, PostgresProvider.Instance);
            await SdetGraphGeneratedConsumer.SeedAsync(db);
            await SdetGraphGeneratedConsumer.AssertReadAsync(db);
        }
        finally
        {
            await using var drop = admin.CreateCommand("DROP DATABASE \"" + name + "\" WITH (FORCE)");
            await drop.ExecuteNonQueryAsync();
        }
    }
}
