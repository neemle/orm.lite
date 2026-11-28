// Provenance (§3.3): ORML-113, FULL, round 1 (§803) - reviewer-authored (codex), screened by QA (triage e45e083a).
// Committed with the F1/F2 reviewer tests; only this header changed.
using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Npgsql;
using Xunit;

namespace Neemle.OrmLite.Tests.Orphans;

internal sealed class OrphanAttackDatabase : IAsyncDisposable, IOrmDiagnosticsSink
{
    private SqliteConnection? _anchor;
    private NpgsqlDataSource? _admin;
    private string? _database;

    internal OrmContext Context { get; private set; } = null!;
    internal ConcurrentQueue<StatementEvent> Statements { get; } = new();

    internal static async Task<OrphanAttackDatabase> OpenAsync(
        string provider, bool logParameters = false)
    {
        Skip.If(provider == "postgres"
            && string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")),
            "ORMLITE_TEST_POSTGRES is not configured.");

        ReplaceModel.Register();
        var db = new OrphanAttackDatabase();
        try
        {
            var connectionString = provider == "postgres"
                ? await db.PostgresAsync()
                : await db.SqliteAsync();

            var options = new OrmLiteOptions
            {
                Provider = provider,
                ConnectionString = connectionString,
                Pooling = false,
            };
            options.Diagnostics.Sink = db;
            options.Diagnostics.StatementLevel = OrmLogLevel.Debug;
            options.Diagnostics.LogParameters = logParameters;

            db.Context = await OrmLite.OpenAsync(options);
            var schema = OrmSchema.Of("attack113",
                ReplaceModel.Parent, ReplaceModel.Child, ReplaceModel.Pin,
                ReplaceModel.Soft, ReplaceModel.Strict);
            var nodes = schema.Baseline().Concat(schema.Indexes()).ToList();
            nodes.Add(RowHistoryDdl.Install(schema.HistoryTables));

            if (provider == "postgres")
            {
                foreach (var sql in DdlCompiler.Render(nodes, db.Context.Provider))
                    await db.ExecuteAsync(sql);
            }
            else
            {
                await MigrationRunner.ApplyAsync(
                    db.Context.DataSource, db.Context.Provider, nodes);
            }

            await db.ExecuteAsync(
                "INSERT INTO orep_parents(id,name) VALUES(@p0,@p1)",
                ReplaceModel.Key(1), "parent");
            db.Statements.Clear();
            return db;
        }
        catch
        {
            await db.DisposeAsync();
            throw;
        }
    }

    private async Task<string> SqliteAsync()
    {
        SqliteProviderFactory.Register();
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = "attack113_" + Guid.NewGuid().ToString("N"),
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            RecursiveTriggers = true,
            Pooling = false,
            DefaultTimeout = 2,
        }.ConnectionString;

        _anchor = new SqliteConnection(connectionString);
        await _anchor.OpenAsync();
        return connectionString;
    }

    private async Task<string> PostgresAsync()
    {
        PostgresProviderFactory.Register();
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")!;
        _admin = NpgsqlDataSource.Create(server);
        var name = "attack113_" + Guid.NewGuid().ToString("N");

        await using (var command =
            _admin.CreateCommand("CREATE DATABASE \"" + name + "\""))
        {
            await command.ExecuteNonQueryAsync();
        }
        _database = name;

        return new NpgsqlConnectionStringBuilder(server)
        {
            Database = name,
            Pooling = false,
            CommandTimeout = 10,
        }.ConnectionString;
    }

    internal async Task<int> ExecuteAsync(string sql, params object?[] values)
    {
        await using var connection =
            await Context.DataSource.OpenConnectionAsync();
        await using var command = MakeCommand(connection, sql, values);
        return await command.ExecuteNonQueryAsync();
    }

    internal async Task<object?> ScalarAsync(string sql, params object?[] values)
    {
        await using var connection =
            await Context.DataSource.OpenConnectionAsync();
        await using var command = MakeCommand(connection, sql, values);
        return await command.ExecuteScalarAsync();
    }

    internal async Task<long> CountAsync(string sql, params object?[] values) =>
        Convert.ToInt64(await ScalarAsync(sql, values));

    internal Task SeedAsync(string table, int id) =>
        ExecuteAsync(
            "INSERT INTO " + table + "(id,parent_id,name) VALUES(@p0,@p1,@p2)",
            ReplaceModel.Key(id), ReplaceModel.Key(1), "child " + id);

    private static DbCommand MakeCommand(
        DbConnection connection, string sql, object?[] values)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < values.Length; i++)
            command.AddParam("p" + i, values[i]);
        return command;
    }

    public void OnStatement(StatementEvent statement) => Statements.Enqueue(statement);
    public void OnTransaction(TransactionEvent transaction) { }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Context is not null) await Context.DisposeAsync();
        }
        finally
        {
            try
            {
                if (_anchor is not null) await _anchor.DisposeAsync();
            }
            finally
            {
                if (_admin is not null)
                {
                    try
                    {
                        if (_database is not null)
                        {
                            await using var command = _admin.CreateCommand(
                                "DROP DATABASE \"" + _database + "\" WITH (FORCE)");
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                    finally
                    {
                        await _admin.DisposeAsync();
                    }
                }
            }
        }
    }
}
