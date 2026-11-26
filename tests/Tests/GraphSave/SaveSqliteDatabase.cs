using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Sqlite;

namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveSqliteDatabase : IAsyncDisposable, IOrmDiagnosticsSink
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gs112-sdet-" + Guid.NewGuid().ToString("N"));
    internal OrmContext Context { get; private set; } = null!;
    internal List<StatementEvent> Statements { get; } = [];
    internal List<TransactionEvent> Transactions { get; } = [];

    internal static async Task<SaveSqliteDatabase> OpenAsync(bool rejectLinks = false)
    {
        SaveProbeModel.Register();
        var result = new SaveSqliteDatabase();
        Directory.CreateDirectory(result._directory);
        SqliteProviderFactory.Register();
        var options = new OrmLiteOptions
        {
            Provider = "sqlite", Pooling = false,
            ConnectionString = new SqliteConnectionStringBuilder
            { DataSource = Path.Combine(result._directory, "save.db"), ForeignKeys = true, Pooling = false }.ConnectionString,
        };
        options.Diagnostics.Sink = result;
        options.Diagnostics.StatementLevel = OrmLogLevel.Debug;
        options.Diagnostics.TransactionLevel = OrmLogLevel.Debug;
        options.Diagnostics.LogParameters = true;
        result.Context = await OrmLite.OpenAsync(options);
        await result.ExecuteAsync("""
            CREATE TABLE gs112_probe_node (
              id TEXT PRIMARY KEY NOT NULL,
              parent_id TEXT NULL REFERENCES gs112_probe_node(id),
              name TEXT NOT NULL CHECK(name <> 'reject'));
            CREATE TABLE gs112_probe_tag (id TEXT PRIMARY KEY NOT NULL, name TEXT NOT NULL);
            """);
        await result.ExecuteAsync("CREATE TABLE gs112_probe_links (node_id TEXT NOT NULL REFERENCES gs112_probe_node(id), "
            + "tag_id TEXT NOT NULL REFERENCES gs112_probe_tag(id), PRIMARY KEY(node_id, tag_id)"
            + (rejectLinks ? ", CHECK(0)" : "") + ");");
        return result;
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
        Directory.Delete(_directory, true);
    }
}
