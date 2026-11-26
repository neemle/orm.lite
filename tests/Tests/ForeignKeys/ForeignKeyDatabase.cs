using System.Data.Common;
using Microsoft.Data.Sqlite;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Sqlite;
using Npgsql;

namespace Neemle.OrmLite.Tests.ForeignKeys;

internal sealed class ForeignKeyDatabase(OrmContext context, NpgsqlDataSource? admin, string storage) : IAsyncDisposable
{
    internal OrmContext Context { get; } = context;

    internal static async Task<ForeignKeyDatabase> SqliteAsync(bool enabled = true)
    {
        var directory = Path.Combine(Path.GetTempPath(), "orml110-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        SqliteProviderFactory.Register();
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "fk.db"), ForeignKeys = enabled, Pooling = false,
        };
        var context = await OrmLite.OpenAsync(new OrmLiteOptions
        {
            Provider = "sqlite", ConnectionString = connection.ConnectionString,
        });
        return new ForeignKeyDatabase(context, null, directory);
    }

    internal static async Task<ForeignKeyDatabase> PostgresAsync()
    {
        var server = Environment.GetEnvironmentVariable("ORMLITE_TEST_POSTGRES")
            ?? throw new InvalidOperationException("ORMLITE_TEST_POSTGRES must name the ephemeral test server.");
        var admin = NpgsqlDataSource.Create(server);
        var name = "orml110_" + Guid.NewGuid().ToString("N");
        try
        {
            await using var create = admin.CreateCommand("CREATE DATABASE \"" + name + "\"");
            await create.ExecuteNonQueryAsync();
            var builder = new NpgsqlConnectionStringBuilder(server) { Database = name };
            var source = NpgsqlDataSource.Create(builder.ConnectionString);
            return new ForeignKeyDatabase(new OrmContext(source, PostgresProvider.Instance), admin, name);
        }
        catch { await admin.DisposeAsync(); throw; }
    }

    private DbCommand Command(string sql, object?[] values)
    {
        var command = Context.DataSource.CreateCommand(sql);
        for (var i = 0; i < values.Length; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "p" + i;
            parameter.Value = values[i] ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    internal async Task<int> ExecuteAsync(string sql, params object?[] values)
    {
        await using var command = Command(sql, values);
        return await command.ExecuteNonQueryAsync();
    }

    internal async Task<object?> ScalarAsync(string sql, params object?[] values)
    {
        await using var command = Command(sql, values);
        return await command.ExecuteScalarAsync();
    }

    internal async Task<IReadOnlyList<CatalogForeignKey>> ForeignKeysAsync(string table)
    {
        await using var connection = await Context.DataSource.OpenConnectionAsync();
        return await Context.Provider.CreateCatalog(connection).ForeignKeysAsync(table);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        if (admin is null) { Directory.Delete(storage, true); return; }
        await Context.DataSource.DisposeAsync();
        try
        {
            await using var remove = admin.CreateCommand("DROP DATABASE \"" + storage + "\" WITH (FORCE)");
            await remove.ExecuteNonQueryAsync();
        }
        finally { await admin.DisposeAsync(); }
    }
}
