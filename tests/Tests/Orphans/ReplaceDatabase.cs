using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Npgsql;

namespace Neemle.OrmLite.Tests.Orphans;

// A fresh SQLite file or PostgreSQL database holding the ReplaceModel tables (generated DDL, native row history for
// the soft child), with a diagnostics sink recording every statement and transaction event.
internal sealed class ReplaceDatabase : IAsyncDisposable, IOrmDiagnosticsSink
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "orep113-" + Guid.NewGuid().ToString("N"));
    private NpgsqlDataSource? _admin;
    private string? _database;
    private OrmLiteOptions _options = null!;
    internal OrmContext Context { get; private set; } = null!;
    internal bool Postgres => _admin is not null;
    internal List<StatementEvent> Statements { get; } = [];
    internal List<TransactionEvent> Transactions { get; } = [];
    internal Action<StatementEvent>? OnWrite { get; set; }

    internal static async Task<ReplaceDatabase> OpenAsync(string provider)
    {
        ReplaceModel.Register();
        var result = new ReplaceDatabase();
        Directory.CreateDirectory(result._directory);
        try { await result.InitializeAsync(provider); return result; }
        catch { await result.DisposeAsync(); throw; }
    }

    private async Task InitializeAsync(string provider)
    {
        var options = new OrmLiteOptions
        {
            Provider = provider, Pooling = false,
            ConnectionString = provider == "postgres" ? await PostgresAsync() : SqliteConnectionString(),
        };
        options.Diagnostics.Sink = this;
        options.Diagnostics.StatementLevel = OrmLogLevel.Debug;
        options.Diagnostics.TransactionLevel = OrmLogLevel.Debug;
        _options = options;
        Context = await OrmLite.OpenAsync(options);
        var schema = OrmSchema.Of("orep113", ReplaceModel.Parent, ReplaceModel.Child, ReplaceModel.Pin,
            ReplaceModel.Soft, ReplaceModel.Strict);
        var nodes = schema.Baseline().Concat(schema.Indexes()).ToList();
        nodes.Add(RowHistoryDdl.Install(schema.HistoryTables));
        if (Postgres)
            foreach (var sql in DdlCompiler.Render(nodes, Context.Provider)) await ExecuteAsync(sql);
        else await MigrationRunner.ApplyAsync(Context.DataSource, Context.Provider, nodes);
        for (var parent = 1; parent <= 2; parent++)
            await ExecuteAsync("INSERT INTO orep_parents(id,name) VALUES(@p0,@p1)", ReplaceModel.Key(parent), "p" + parent);
        ClearEvents();
    }

    // A second context over the same database whose provider is StaleGuardProvider: the real provider and guard, plus
    // one statement run inside the scope after the guard verified. Built the way OrmLite.OpenAsync builds a context.
    internal async Task<OrmContext> StaleContextAsync(string sql, params object?[] values)
    {
        var factory = ProviderRegistry.Resolve(_options);
        var provider = new StaleGuardProvider(factory.CreateProvider(_options), sql, values);
        var lanes = new[] { DbLaneKind.Request, DbLaneKind.Background, DbLaneKind.Ops }
            .Select(lane => factory.CreateDataSource(_options, lane)).ToArray();
        await factory.InitializeAsync(_options, lanes[1], CancellationToken.None);
        var gated = new GatedDataSource(provider, lanes[0], lanes[1], lanes[2],
            OrmDiagnostics.Create(provider, _options.Diagnostics));
        return new OrmContext(gated, provider, lanes.Distinct().Cast<IAsyncDisposable>().ToList());
    }

    private string SqliteConnectionString()
    {
        SqliteProviderFactory.Register();
        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_directory, "orep.db"), ForeignKeys = true, Pooling = false, DefaultTimeout = 1,
        }.ConnectionString;
    }

    private async Task<string> PostgresAsync()
    {
        PostgresProviderFactory.Register();
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
            ?? throw new InvalidOperationException("ORMLITE_TEST_POSTGRES must name the ephemeral test server.");
        _admin = NpgsqlDataSource.Create(server);
        _database = "orep113_" + Guid.NewGuid().ToString("N");
        await using var create = _admin.CreateCommand("CREATE DATABASE \"" + _database + "\"");
        await create.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(server) { Database = _database, Pooling = false }.ConnectionString;
    }

    internal async Task<int> ExecuteAsync(string sql, params object?[] values)
    {
        await using var connection = await Context.DataSource.OpenConnectionAsync();
        return await RunAsync(connection, sql, values);
    }

    private static async Task<int> RunAsync(DbConnection connection, string sql, object?[] values)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < values.Length; i++) command.AddParam("p" + i, values[i]);
        return await command.ExecuteNonQueryAsync();
    }

    // A second writer on its own connection that gives up quickly when a lock or the SQLite writer slot is held.
    internal async Task<Exception?> SideWriteAsync(string sql, params object?[] values)
    {
        await using var connection = await Context.DataSource.OpenConnectionAsync();
        try
        {
            if (Postgres) await RunAsync(connection, "SET lock_timeout = '400ms'", []);
            await RunAsync(connection, sql, values);
            return null;
        }
        catch (Exception error) { return error; }
    }

    internal async Task<IReadOnlyList<(Guid Id, Guid? ParentId, string Name)>> ChildrenAsync(string table = "orep_children")
    {
        await using var connection = await Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id,parent_id,name FROM " + table + " ORDER BY id";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<(Guid, Guid?, string)>();
        while (await reader.ReadAsync())
            rows.Add((reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetString(2)));
        return rows;
    }

    internal async Task<long> CountAsync(string sql, params object?[] values)
    {
        await using var connection = await Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < values.Length; i++) command.AddParam("p" + i, values[i]);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    internal async Task SeedAsync(string table, int id, int? parent, string name)
    {
        object? owner = parent is { } p ? ReplaceModel.Key(p) : null;
        await ExecuteAsync("INSERT INTO " + table + "(id,parent_id,name) VALUES(@p0,@p1,@p2)",
            ReplaceModel.Key(id), owner, name);
    }

    public void OnStatement(StatementEvent statement)
    {
        lock (Statements) Statements.Add(statement);
        if (statement.Kind == StatementKind.Write) OnWrite?.Invoke(statement);
    }

    public void OnTransaction(TransactionEvent transaction) { lock (Transactions) Transactions.Add(transaction); }
    internal void ClearEvents() { lock (Statements) Statements.Clear(); lock (Transactions) Transactions.Clear(); }

    public async ValueTask DisposeAsync()
    {
        if (Context is not null) await Context.DisposeAsync();
        try
        {
            if (_admin is null) return;
            await using var drop = _admin.CreateCommand("DROP DATABASE \"" + _database + "\" WITH (FORCE)");
            await drop.ExecuteNonQueryAsync();
        }
        finally
        {
            if (_admin is not null) await _admin.DisposeAsync();
            Directory.Delete(_directory, true);
        }
    }
}
