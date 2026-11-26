using System.Data.Common;
using Microsoft.Data.Sqlite;

namespace Neemle.OrmLite.Sqlite;

internal static class SqliteSessionCommands
{
    internal static async Task<object?> RunAsync(DbConnection connection, string sql, bool scalar, CancellationToken ct)
    {
        var resources = new SqliteMetadataResources();
        object? result = null; Exception? primary = null;
        try
        {
            var command = connection.CreateCommand(); resources.Add(command);
            command.CommandText = sql;
            result = scalar ? await command.ExecuteScalarAsync(ct) : await command.ExecuteNonQueryAsync(ct);
        }
        catch (Exception error) { primary = error; }
        await resources.FinishAsync(primary, ct);
        return result;
    }
    internal static async Task<SqliteConnection> NativeAsync(DbConnection connection, CancellationToken ct)
    {
        var resources = new SqliteMetadataResources();
        SqliteConnection? native = null; Exception? primary = null;
        try
        {
            var command = connection.CreateCommand(); resources.Add(command);
            native = command.Connection as SqliteConnection
                ?? throw new SchemaDiffException(SchemaDiffError.UnsupportedProvider);
        }
        catch (Exception error) { primary = error; }
        await resources.FinishAsync(primary, ct);
        return native!;
    }

}
