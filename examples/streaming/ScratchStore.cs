using Neemle.OrmLite;
using Npgsql;

namespace StreamingExample;

internal sealed class ScratchStore : IAsyncDisposable
{
    private NpgsqlDataSource? _admin;
    private string? _database;
    private string? _directory;
    private OrmContext? _context;
    internal OrmContext Context => _context ?? throw new InvalidOperationException("Store is not open.");
    internal bool CleanupSucceeded { get; private set; } = true;

    internal async Task OpenAsync(string? server)
    {
        if (server is null)
        {
            _directory = Directory.CreateTempSubdirectory("ormlite-streaming-").FullName;
            _context = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite",
                ConnectionString = "Data Source=" + Path.Combine(_directory, "notes.db") + ";Pooling=False" });
            return;
        }
        _admin = NpgsqlDataSource.Create(server);
        var name = "streaming_example_" + Guid.NewGuid().ToString("N");
        await using var create = _admin.CreateCommand("CREATE DATABASE \"" + name + "\"");
        await create.ExecuteNonQueryAsync();
        _database = name;
        var connection = new NpgsqlConnectionStringBuilder(server) { Database = name };
        _context = await OrmLite.OpenAsync(new OrmLiteOptions
            { Provider = "postgres", ConnectionString = connection.ConnectionString });
    }

    public async ValueTask DisposeAsync()
    {
        if (_context is { } context) await AttemptAsync(context.DisposeAsync);
        if (_admin is { } admin)
        {
            if (_database is { } database) await AttemptAsync(() => DropAsync(admin, database));
            await AttemptAsync(admin.DisposeAsync);
        }
        if (_directory is { } directory)
        {
            try { Directory.Delete(directory, true); }
            catch (Exception) { CleanupSucceeded = false; }
        }
        _context = null; _admin = null; _database = null; _directory = null;
    }

    private static async ValueTask DropAsync(NpgsqlDataSource admin, string database)
    {
        await using var drop = admin.CreateCommand("DROP DATABASE \"" + database + "\" WITH (FORCE)");
        await drop.ExecuteNonQueryAsync();
    }

    private async ValueTask AttemptAsync(Func<ValueTask> cleanup)
    {
        try { await cleanup(); }
        catch (Exception) { CleanupSucceeded = false; }
    }
}
