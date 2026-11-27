using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;
using Neemle.OrmLite.Postgres;
using Npgsql;

namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveProviderDatabase : IAsyncDisposable, IOrmDiagnosticsSink
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gs112-sdet-" + Guid.NewGuid().ToString("N"));
    internal OrmContext Context { get; private set; } = null!;
    internal List<StatementEvent> Statements { get; } = [];
    internal List<TransactionEvent> Transactions { get; } = [];
    private NpgsqlDataSource? _admin;
    private string? _database;

    internal static async Task<SaveProviderDatabase> OpenAsync(string provider, bool rejectLinks = false)
    {
        SaveProbeModel.Register();
        var result = new SaveProviderDatabase();
        Directory.CreateDirectory(result._directory);
        SqliteProviderFactory.Register();
        var connectionString = provider == "sqlite"
            ? new SqliteConnectionStringBuilder
                { DataSource = Path.Combine(result._directory, "save.db"), ForeignKeys = true, Pooling = false }.ConnectionString
            : await result.PostgresConnectionAsync();
        var options = new OrmLiteOptions
        {
            Provider = provider, Pooling = false, ConnectionString = connectionString,
        };
        options.Diagnostics.Sink = result;
        options.Diagnostics.StatementLevel = OrmLogLevel.Debug;
        options.Diagnostics.TransactionLevel = OrmLogLevel.Debug;
        options.Diagnostics.LogParameters = true;
        result.Context = await OrmLite.OpenAsync(options);
        var keyType = provider == "sqlite" ? "TEXT" : "uuid";
        await result.ExecuteAsync("CREATE TABLE gs112_probe_node (id " + keyType + " PRIMARY KEY NOT NULL, parent_id "
            + keyType + " NULL REFERENCES gs112_probe_node(id), name TEXT NOT NULL CHECK(name <> 'reject'));"
            + "CREATE TABLE gs112_probe_tag (id " + keyType + " PRIMARY KEY NOT NULL, name TEXT NOT NULL);");
        await result.ExecuteAsync("CREATE TABLE gs112_probe_links (node_id " + keyType + " NOT NULL REFERENCES gs112_probe_node(id), "
            + "tag_id " + keyType + " NOT NULL REFERENCES gs112_probe_tag(id), PRIMARY KEY(node_id, tag_id)"
            + (rejectLinks ? ", CHECK(1=0)" : "") + ");");
        return result;
    }

    private async Task<string> PostgresConnectionAsync()
    {
        PostgresProviderFactory.Register();
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
            ?? throw new InvalidOperationException("An isolated PostgreSQL fixture endpoint is required.");
        _admin = NpgsqlDataSource.Create(server);
        _database = "gs112_" + Guid.NewGuid().ToString("N");
        await using var create = _admin.CreateCommand("CREATE DATABASE \"" + _database + "\"");
        await create.ExecuteNonQueryAsync();
        return new NpgsqlConnectionStringBuilder(server) { Database = _database, Pooling = false }.ConnectionString;
    }

    internal async Task ExecuteAsync(string sql)
    {
        await using var connection = await Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    internal async Task<object?> ScalarAsync(string sql, object? value = null)
    {
        await using var connection = await Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (value is not null)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "p";
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return await command.ExecuteScalarAsync();
    }

    public void OnStatement(StatementEvent statement) => Statements.Add(statement);
    public void OnTransaction(TransactionEvent transaction) => Transactions.Add(transaction);
    internal void ClearEvents() { Statements.Clear(); Transactions.Clear(); }
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
