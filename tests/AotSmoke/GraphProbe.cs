using Microsoft.Data.Sqlite;
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using Neemle.OrmLite.Sqlite;

namespace AotSmoke;

internal static class GraphProbe
{
    internal static async Task<bool> RunAsync()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aot-graph-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        SqliteProviderFactory.Register();
        try
        {
            await using var db = await OrmLite.OpenAsync(new OrmLiteOptions
                { Provider = "sqlite", Path = Path.Combine(directory, "graph.db") });
            await SeedAsync(db);
            var request = new GraphRequest<AotGraphAuthor>(
                new(new(3, 3, 4), new(2, 20, 8)), AotGraphAuthorOrm.Paths.Books,
                AotGraphAuthorOrm.Paths.Tags, AotGraphAuthorOrm.Paths.Books.Then(AotGraphBookOrm.Paths.Editor));
            var roots = await AotGraphAuthorOrm.Query(db).OrderBy(a => a.Id).ToGraphListAsync(request);
            return roots.Count == 2 && roots[0].Books.Count == 1 && roots[1].Books.Count == 1
                && ReferenceEquals(roots[1], roots[0].Books[0].Editor)
                && roots[1].Books[0].Editor is null
                && roots[0].Tags.Count == 1 && ReferenceEquals(roots[0].Tags[0], roots[1].Tags.Single());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }

    private static async Task SeedAsync(OrmContext db)
    {
        await using var connection = await db.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE aot_graph_authors (id INTEGER PRIMARY KEY);
            CREATE TABLE aot_graph_books (id INTEGER PRIMARY KEY, aot_graph_author_id INTEGER, editor_id INTEGER NULL);
            CREATE TABLE aot_graph_tags (id INTEGER PRIMARY KEY);
            CREATE TABLE aot_graph_links (author_id INTEGER, tag_id INTEGER);
            INSERT INTO aot_graph_authors VALUES (1), (2);
            INSERT INTO aot_graph_books VALUES (10, 1, 2), (11, 2, NULL);
            INSERT INTO aot_graph_tags VALUES (30);
            INSERT INTO aot_graph_links VALUES (1, 30), (2, 30);
            """;
        await command.ExecuteNonQueryAsync();
    }
}
