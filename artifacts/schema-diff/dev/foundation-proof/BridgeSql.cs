using System.Data.Common;

namespace FoundationProof;

internal static class BridgeSql
{
    internal static string Quoted(string name) => new Neemle.OrmLite.Sql.SqlWriter().Ident(name).ToString();

    internal static async Task ExecuteAsync(DbDataSource source, string sql)
    {
        await using var connection = await source.OpenConnectionAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    internal static async Task<object?> ScalarAsync(DbDataSource source, string sql)
    {
        await using var connection = await source.OpenConnectionAsync();
        await using var command = connection.CreateCommand(); command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
