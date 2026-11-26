using Neemle.OrmLite;
using Npgsql;

namespace OrmLiteDemo;

internal sealed class GraphDemoDatabase : IAsyncDisposable
{
    private NpgsqlDataSource? _admin;
    private string? _database;
    private string? _directory;
    internal OrmContext Context { get; private set; } = null!;
    internal bool CleanupSucceeded { get; private set; } = true;

    internal static async Task<GraphDemoDatabase> OpenAsync(string provider)
    {
        var scratch = new GraphDemoDatabase();
        try
        {
            Neemle.OrmLite.Sqlite.SqliteProviderFactory.Register();
            Neemle.OrmLite.Postgres.PostgresProviderFactory.Register();
            scratch.Context = await OrmLite.OpenAsync(await scratch.OptionsAsync(provider));
            return scratch;
        }
        catch { await scratch.DisposeAsync(); throw; }
    }

    private async Task<OrmLiteOptions> OptionsAsync(string provider)
    {
        if (provider == "sqlite")
        {
            _directory = Directory.CreateTempSubdirectory("graph-chapter-").FullName;
            return new() { Provider = "sqlite", Path = Path.Combine(_directory, "graph.db"), Pooling = false };
        }
        if (provider != "postgres") throw new ArgumentException("Use sqlite or postgres");
        var server = Environment.GetEnvironmentVariable("ORMLITE_GRAPH_POSTGRES")
            ?? throw new InvalidOperationException("A scratch PostgreSQL server is required");
        _admin = NpgsqlDataSource.Create(server);
        var name = "graph_chapter_" + Guid.NewGuid().ToString("N");
        await using var connection = await _admin.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE DATABASE \"" + name + "\"";
        await command.ExecuteNonQueryAsync();
        _database = name;
        return new() { Provider = "postgres", ConnectionString =
            new NpgsqlConnectionStringBuilder(server) { Database = name, Pooling = false }.ConnectionString };
    }

    private async Task DropAsync()
    {
        await using var connection = await _admin!.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP DATABASE IF EXISTS \"" + _database + "\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    private async Task AttemptAsync(Func<Task> action)
    {
        try { await action(); }
        catch { CleanupSucceeded = false; }
    }

    public async ValueTask DisposeAsync()
    {
        if (Context is not null) await AttemptAsync(() => Context.DisposeAsync().AsTask());
        if (_database is not null) await AttemptAsync(DropAsync);
        if (_admin is not null) await AttemptAsync(() => _admin.DisposeAsync().AsTask());
        if (_directory is not null)
        {
            try { Directory.Delete(_directory, true); }
            catch { CleanupSucceeded = false; }
        }
    }
}
