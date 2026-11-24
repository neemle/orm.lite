using Microsoft.Data.Sqlite;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Sqlite;
using Npgsql;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Behaviour;

public abstract class ProviderFixture : IAsyncLifetime
{
    public abstract string ProviderId { get; }

    public virtual string? UnavailableReason => null;

    public OrmContext Context { get; protected set; } = null!;

    public async Task InitializeAsync()
    {
        BehaveWorld.Register();
        if (UnavailableReason is not null) return;
        await StartAsync();
        foreach (var statement in Schema()) await ExecuteAsync(statement);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (UnavailableReason is not null) return;
        if (Context is not null) await Context.DisposeAsync();
        await CleanUpAsync();
    }

    public void RequireAvailable() => Skip.IfNot(UnavailableReason is null, UnavailableReason ?? "");

    public async Task<int> ExecuteAsync(string statement)
    {
        await using var connection = await Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<List<object?[]>> RowsAsync(string statement)
    {
        await using var connection = await Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<object?[]>();
        while (await reader.ReadAsync())
        {
            var row = new object?[reader.FieldCount];
            for (var i = 0; i < row.Length; i++) row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public abstract OrmLiteOptions NewOptions();

    public abstract int SlowThresholdMilliseconds { get; }

    public abstract string SlowSql { get; }

    public string FastSql => "SELECT 1";

    public string FailingSql => "SELECT * FROM table_that_does_not_exist";

    protected abstract Task StartAsync();

    protected abstract Task CleanUpAsync();

    protected abstract IReadOnlyList<string> Schema();
}

public sealed class SqliteFixture : ProviderFixture
{
    private string _directory = "";

    public override string ProviderId => "sqlite";

    public string DatabasePath => Path.Combine(_directory, "behaviour.db");

    public override OrmLiteOptions NewOptions() => new() { Provider = "sqlite", Path = DatabasePath };

    public override int SlowThresholdMilliseconds => 150;

    public override string SlowSql => "WITH RECURSIVE c(x) AS (SELECT 1 UNION ALL SELECT x + 1 FROM c WHERE x < 6000000) SELECT count(*) FROM c";

    protected override Task CleanUpAsync()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        return Task.CompletedTask;
    }

    protected override async Task StartAsync()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ormlite-behaviour-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        SqliteProviderFactory.Register();
        Context = await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = DatabasePath });
    }

    protected override IReadOnlyList<string> Schema() =>
    [
        "CREATE TABLE behave_notes (id TEXT PRIMARY KEY, tenant_id TEXT NOT NULL, title TEXT NOT NULL, stars INTEGER NOT NULL, price TEXT COLLATE orm_decimal CHECK (orm_decimal_fits(price, 12, 4)) NULL, stamped_at TEXT NULL, deleted_at TEXT NULL, doc TEXT NULL CHECK (json_valid(doc)))",
        "CREATE UNIQUE INDEX behave_notes_live_title ON behave_notes (tenant_id, title) WHERE deleted_at IS NULL",
        "CREATE TABLE behave_counters (id TEXT PRIMARY KEY, n INTEGER NOT NULL)",
    ];
}

public sealed class PostgresFixture : ProviderFixture
{
    public const string Variable = "ORMLITE_TEST_POSTGRES";

    private readonly string _database = "ormlite_behaviour_" + Guid.NewGuid().ToString("N");

    private string _connectionString = "";

    private static string? Server => Environment.GetEnvironmentVariable(Variable);

    public override string ProviderId => "postgres";

    public override OrmLiteOptions NewOptions() => new() { Provider = "postgres", ConnectionString = _connectionString };

    public override int SlowThresholdMilliseconds => 200;

    public override string SlowSql => "SELECT 1 FROM pg_sleep(0.4)";

    public override string? UnavailableReason =>
        string.IsNullOrWhiteSpace(Server)
            ? $"the PostgreSQL half of the behavioural suite is skipped: set {Variable} to a server connection string (for example Host=127.0.0.1;Username=postgres) to run it."
            : null;

    protected override async Task CleanUpAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(Server);
        await admin.OpenAsync();
        await using var command = admin.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    protected override async Task StartAsync()
    {
        await using (var admin = new NpgsqlConnection(Server))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{_database}\"";
            await create.ExecuteNonQueryAsync();
        }
        PostgresProviderFactory.Register();
        var builder = new NpgsqlConnectionStringBuilder(Server) { Database = _database };
        _connectionString = builder.ConnectionString;
        Context = await OrmLite.OpenAsync(NewOptions());
    }

    protected override IReadOnlyList<string> Schema() =>
    [
        "CREATE TABLE behave_notes (id uuid PRIMARY KEY, tenant_id uuid NOT NULL, title text NOT NULL, stars integer NOT NULL, price numeric(12,4) NULL, stamped_at timestamptz NULL, deleted_at timestamptz NULL, doc jsonb NULL)",
        "CREATE UNIQUE INDEX behave_notes_live_title ON behave_notes (tenant_id, title) WHERE deleted_at IS NULL",
        "CREATE TABLE behave_counters (id uuid PRIMARY KEY, n integer NOT NULL)",
    ];
}
