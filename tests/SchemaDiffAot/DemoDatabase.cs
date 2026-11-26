using System.Data.Common;
using System.Globalization;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Npgsql;

namespace Neemle.OrmLite.SchemaDiffAot;

internal static class DemoDatabase
{
    internal static async Task<OrmContext> OpenAsync(string provider, string address)
    {
        if (provider == "postgres")
        {
            var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
                ?? throw new InvalidOperationException("isolated PostgreSQL fixture is required");
            await CreateOwnedDatabaseAsync(server, address);
            return new(NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(server)
                { Database = address, Pooling = false }.ConnectionString), PostgresProvider.Instance);
        }
        if (provider != "sqlite") throw new ArgumentException("provider must be postgres or sqlite");
        SqliteProviderFactory.Register();
        return await OrmLite.OpenAsync(new OrmLiteOptions { Provider = "sqlite", Path = address, Pooling = false });
    }

    private static async Task CreateOwnedDatabaseAsync(string server, string address)
    {
        if (!address.StartsWith("parity117_", StringComparison.Ordinal)
            || !address.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new ArgumentException("owned fixture database name is required");
        await using var source = NpgsqlDataSource.Create(server);
        await using var connection = await source.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE DATABASE \"" + address + "\"";
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task ExecuteAsync(OrmContext context, string sql)
    {
        await using var connection = await context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task<string> ScalarAsync(OrmContext context, string sql)
    {
        await using var connection = await context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? "NULL" : Convert.ToString(value, CultureInfo.InvariantCulture)!;
    }

    internal static async Task<string> HistoryAsync(OrmContext context)
    {
        await using var connection = await context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_migrations ORDER BY version";
        await using var reader = await command.ExecuteReaderAsync();
        var versions = new List<string>();
        while (await reader.ReadAsync()) versions.Add(reader.GetString(0));
        return string.Join("|", versions);
    }
}
