using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Generated;
using Npgsql;

namespace OneToOneDemo;

internal sealed class Fixture : IAsyncDisposable
{
    private NpgsqlDataSource? _admin;
    private string? _database;
    private string? _folder;
    internal OrmContext Db { get; private set; } = null!;
    internal SqlCounter Counter { get; } = new();
    internal bool Cleaned { get; private set; } = true;

    internal static async Task<Fixture> OpenAsync(string provider)
    {
        var fixture = new Fixture();
        try
        {
            var options = await fixture.OptionsAsync(provider);
            options.Diagnostics.Sink = fixture.Counter;
            options.Diagnostics.StatementLevel = OrmLogLevel.Debug;
            fixture.Db = await Neemle.OrmLite.OrmLite.OpenAsync(options);
            await fixture.Db.MigrateAsync(OneToOneDemoSchema.Instance.Contributions());
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    private async Task<OrmLiteOptions> OptionsAsync(string provider)
    {
        if (provider == "sqlite")
        {
            _folder = Directory.CreateTempSubdirectory("one-to-one-").FullName;
            return new OrmLiteOptions { Provider = provider, Path = Path.Combine(_folder, "demo.db"), Pooling = false };
        }
        if (provider != "postgres") throw new ArgumentException("Use sqlite or postgres");
        var source = Environment.GetEnvironmentVariable("ONE_TO_ONE_POSTGRES")
            ?? throw new InvalidOperationException("Set ONE_TO_ONE_POSTGRES for an owned scratch database");
        _admin = NpgsqlDataSource.Create(source);
        _database = "one_to_one_" + Guid.NewGuid().ToString("N");
        await using var command = _admin.CreateCommand("CREATE DATABASE \"" + _database + "\"");
        await command.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(source) { Database = _database, Pooling = false };
        return new OrmLiteOptions { Provider = provider, ConnectionString = builder.ConnectionString };
    }

    internal async Task<object?> ScalarAsync(string sql)
    {
        await using var command = Db.DataSource.CreateCommand(sql);
        return await command.ExecuteScalarAsync();
    }

    internal async Task ExecuteAsync(string sql)
    {
        await using var command = Db.DataSource.CreateCommand(sql);
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Db is not null) await Attempt(Db.DisposeAsync);
        if (_admin is not null && _database is not null) await Attempt(DropAsync);
        if (_admin is not null) await Attempt(_admin.DisposeAsync);
        if (_folder is not null) await Attempt(() =>
            { Directory.Delete(_folder, true); return ValueTask.CompletedTask; });
    }

    private async ValueTask DropAsync()
    {
        await using var command = _admin!.CreateCommand("DROP DATABASE \"" + _database + "\" WITH (FORCE)");
        await command.ExecuteNonQueryAsync();
    }

    private async ValueTask Attempt(Func<ValueTask> action)
    {
        try { await action(); }
        catch { Cleaned = false; }
    }
}
