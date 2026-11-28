using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Generated;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Npgsql;

namespace Neemle.OrmLite.Tests.Integration.Joins;

// A fresh database per test: a SQLite file in a temporary directory, or a PostgreSQL database created on the server
// at ORMLITE_TEST_POSTGRES and dropped afterwards. Three authors and three books: Aster wrote Alpha (no editor) and
// Beta (edited by Birch), Birch wrote Gamma, Cedar wrote nothing.
internal sealed class JoinDatabase : IAsyncDisposable
{
    internal static readonly Guid Aster = Guid.Parse("10000000-0000-0000-0000-000000000001");
    internal static readonly Guid Birch = Guid.Parse("10000000-0000-0000-0000-000000000002");
    internal static readonly Guid Cedar = Guid.Parse("10000000-0000-0000-0000-000000000003");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "it-joins-" + Guid.NewGuid().ToString("N"));
    private NpgsqlDataSource? _admin;
    private string? _database;

    internal OrmContext Context { get; private set; } = null!;

    internal static async Task<JoinDatabase> OpenAsync(string provider)
    {
        var db = new JoinDatabase();
        Directory.CreateDirectory(db._directory);
        SqliteProviderFactory.Register();
        PostgresProviderFactory.Register();
        var connectionString = provider == "sqlite"
            ? new SqliteConnectionStringBuilder { DataSource = Path.Combine(db._directory, "joins.db"), Pooling = false }
                .ConnectionString
            : await db.CreatePostgresAsync();
        db.Context = await OrmLite.OpenAsync(new OrmLiteOptions
            { Provider = provider, Pooling = false, ConnectionString = connectionString });
        await db.SeedAsync();
        return db;
    }

    private async Task<string> CreatePostgresAsync()
    {
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")!;
        _admin = NpgsqlDataSource.Create(server);
        _database = "it_joins_" + Guid.NewGuid().ToString("N");
        await using var create = _admin.CreateCommand("CREATE DATABASE \"" + _database + "\"");
        await create.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(server) { Database = _database, Pooling = false }.ConnectionString;
    }

    private async Task SeedAsync()
    {
        var schema = OrmSchema.Of("it_joins", JoinAuthorOrm.Descriptor, JoinBookOrm.Descriptor);
        await Context.MigrateAsync(schema.Contributions());
        foreach (var (id, name) in new[] { (Aster, "Aster"), (Birch, "Birch"), (Cedar, "Cedar") })
            await JoinAuthorOrm.InsertAsync(Context, new JoinAuthor { Id = id, Name = name });
        await Book("20000000-0000-0000-0000-000000000001", Aster, "Alpha", null);
        await Book("20000000-0000-0000-0000-000000000002", Aster, "Beta", Birch);
        await Book("20000000-0000-0000-0000-000000000003", Birch, "Gamma", null);
    }

    private Task Book(string id, Guid author, string title, Guid? editor) => JoinBookOrm.InsertAsync(Context,
        new JoinBook { Id = Guid.Parse(id), AuthorId = author, Title = title, EditorId = editor });

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        try
        {
            if (_admin is not null)
            {
                await using var drop = _admin.CreateCommand("DROP DATABASE \"" + _database + "\" WITH (FORCE)");
                await drop.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            if (_admin is not null) await _admin.DisposeAsync();
            Directory.Delete(_directory, true);
        }
    }
}
