using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Tests.Nodes;
using Npgsql;
using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphPostgresTests
{
    [PostgresFact(DisplayName = "[UC-002 G5] PostgreSQL preserves graph membership, root page and shared identity")]
    public async Task PostgreSql_round_trip_uses_the_same_static_graph_contract()
    {
        GraphDomain.Register();
        var server = Environment.GetEnvironmentVariable(PostgresFactAttribute.Variable)!;
        await using var admin = NpgsqlDataSource.Create(server);
        var name = "orm_graph_" + Guid.NewGuid().ToString("N");
        await using (var create = admin.CreateCommand("CREATE DATABASE \"" + name + "\""))
            await create.ExecuteNonQueryAsync();
        try { await VerifyAsync(new NpgsqlConnectionStringBuilder(server) { Database = name }.ConnectionString); }
        finally
        {
            await using var drop = admin.CreateCommand("DROP DATABASE \"" + name + "\" WITH (FORCE)");
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task VerifyAsync(string connectionString)
    {
        await using var source = NpgsqlDataSource.Create(connectionString);
        await using var db = new OrmContext(source, PostgresProvider.Instance);
        await using (var seed = source.CreateCommand(GraphDomain.Schema)) await seed.ExecuteNonQueryAsync();
        var request = new GraphRequest<GraphAuthor>(GraphDomain.Limits(), GraphDomain.AuthorTags,
            GraphDomain.AuthorBooks.Then(GraphDomain.BookEditor));
        var roots = await new Query<GraphAuthor>(db, GraphDomain.MapAuthor)
            .OrderBy(a => a.Id).Take(2).ToGraphListAsync(request);
        Assert.Equal(new[] { 1, 2 }, roots.Select(a => a.Id));
        Assert.Equal(new[] { 10, 11 }, roots[0].Books.Select(b => b.Id));
        Assert.Same(roots[1], roots[0].Books[0].Editor);
        Assert.Null(roots[0].Books[1].Editor);
        Assert.Null(roots[1].Books[1].Editor);
        Assert.Same(roots[0].Tags.Single(), roots[1].Tags.Single());
    }
}
