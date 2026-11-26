using Neemle.OrmLite;

namespace PolicySmoke;

internal sealed class Fixture(OrmContext context, SpySink sink, string? sqlitePath) : IAsyncDisposable
{
    internal OrmContext Context { get; } = context;
    internal SpySink Sink { get; } = sink;
    internal static GraphLimits Limits => new(new(8, 32, 64), new(100, 1000, 64));
    internal static LoadingScopeOptions Options => new(Limits, 4, TimeSpan.FromMinutes(2));

    internal static async Task<Fixture> OpenAsync(string provider)
    {
        Neemle.OrmLite.Sqlite.SqliteProviderFactory.Register();
        Neemle.OrmLite.Postgres.PostgresProviderFactory.Register();
        var path = provider == "sqlite" ? Path.Combine(Path.GetTempPath(), "policy-" + Guid.NewGuid() + ".db") : null;
        var options = new OrmLiteOptions { Provider = provider, Path = path };
        if (provider == "postgres") options.ConnectionString = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
            ?? throw new InvalidOperationException("PostgreSQL connection missing");
        var sink = new SpySink();
        options.Diagnostics.Sink = sink;
        options.Diagnostics.StatementLevel = OrmLogLevel.Debug;
        options.Diagnostics.TransactionLevel = OrmLogLevel.Debug;
        var context = await Neemle.OrmLite.OrmLite.OpenAsync(options);
        var fixture = new Fixture(context, sink, path);
        await fixture.SeedAsync();
        return fixture;
    }

    private async Task SeedAsync()
    {
        await using var connection = await Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DROP TABLE IF EXISTS policy_author_tags;
            DROP TABLE IF EXISTS policy_books;
            DROP TABLE IF EXISTS policy_tags;
            DROP TABLE IF EXISTS policy_authors;
            DROP TABLE IF EXISTS policy_plain;
            CREATE TABLE policy_authors(id INTEGER PRIMARY KEY, name TEXT NULL);
            CREATE TABLE policy_books(id INTEGER PRIMARY KEY, author_id INTEGER NOT NULL, editor_id INTEGER NULL);
            CREATE TABLE policy_tags(id INTEGER PRIMARY KEY, name TEXT NULL);
            CREATE TABLE policy_author_tags(author_id INTEGER NOT NULL, tag_id INTEGER NOT NULL);
            CREATE TABLE policy_plain(id INTEGER PRIMARY KEY);
            INSERT INTO policy_authors VALUES (1,'A'),(2,'B'),(3,'C');
            INSERT INTO policy_books VALUES (10,1,2),(11,1,NULL),(12,2,1);
            INSERT INTO policy_tags VALUES (20,'T'),(21,'U');
            INSERT INTO policy_author_tags VALUES (1,20),(1,21),(2,20);
            INSERT INTO policy_plain VALUES (1),(2);
            """;
        await command.ExecuteNonQueryAsync();
        Sink.Clear();
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        if (sqlitePath is not null)
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(sqlitePath + suffix);
    }
}
