using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Tests.Nodes;
using Npgsql;

namespace Neemle.OrmLite.Tests.Maintenance;

// A throwaway PostgreSQL database for one test, opened through OrmLite.OpenAsync with a recording sink. Only reached
// from [PostgresFact] tests, which skip without ORMLITE_TEST_POSTGRES.
internal sealed class PostgresMaintenanceDatabase : IAsyncDisposable
{
    private readonly string _database;

    private PostgresMaintenanceDatabase(string database, string connectionString, MaintenanceSink sink, OrmContext context)
    {
        _database = database;
        ConnectionString = connectionString;
        Sink = sink;
        Context = context;
    }

    private static string Server => Environment.GetEnvironmentVariable(PostgresFactAttribute.Variable)
        ?? throw new InvalidOperationException("set " + PostgresFactAttribute.Variable);

    internal string ConnectionString { get; }

    internal MaintenanceSink Sink { get; }

    internal OrmContext Context { get; }

    internal OrmMaintenance Maintenance => Context.Maintenance;

    internal static async Task<PostgresMaintenanceDatabase> CreateAsync()
    {
        var database = "ormlite_maint_" + Guid.NewGuid().ToString("N");
        await using (var admin = new NpgsqlConnection(Server))
        {
            await admin.OpenAsync();
            await using var create = admin.CreateCommand();
            create.CommandText = "CREATE DATABASE \"" + database + "\"";
            await create.ExecuteNonQueryAsync();
        }
        PostgresProviderFactory.Register();
        var connectionString = new NpgsqlConnectionStringBuilder(Server) { Database = database }.ConnectionString;
        var sink = new MaintenanceSink();
        var options = new OrmLiteOptions { Provider = "postgres", ConnectionString = connectionString };
        options.Diagnostics.Sink = sink;
        options.Diagnostics.StatementLevel = OrmLogLevel.Information;
        return new PostgresMaintenanceDatabase(database, connectionString, sink, await OrmLite.OpenAsync(options));
    }

    internal async Task<NpgsqlConnection> RawAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    internal async Task ExecAsync(string sql)
    {
        await using var connection = await RawAsync();
        await SqliteMaintenanceDatabase.Exec(connection, sql);
    }

    internal async Task<object?> ScalarAsync(string sql)
    {
        await using var connection = await RawAsync();
        return await SqliteMaintenanceDatabase.Scalar(connection, sql);
    }

    internal async Task<long> RelFileNodeAsync(string relation) =>
        Convert.ToInt64(await ScalarAsync("SELECT relfilenode FROM pg_class WHERE relname = '" + relation + "'"),
            System.Globalization.CultureInfo.InvariantCulture);

    internal async Task<List<string>> DumpAsync(string sql)
    {
        await using var connection = await RawAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync())
        {
            var cells = new object?[reader.FieldCount];
            for (var i = 0; i < cells.Length; i++) cells[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(string.Join("|", cells.Select(c => Convert.ToString(c, System.Globalization.CultureInfo.InvariantCulture))));
        }
        return rows;
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(Server);
        await admin.OpenAsync();
        await using var drop = admin.CreateCommand();
        drop.CommandText = "DROP DATABASE IF EXISTS \"" + _database + "\" WITH (FORCE)";
        await drop.ExecuteNonQueryAsync();
    }
}
