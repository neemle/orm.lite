using Neemle.OrmLite;

namespace ForeignKeysAot;

internal static class NativeCommands
{
    internal static async Task<object?> ScalarAsync(OrmContext db, string sql, params object?[] values)
    {
        await using var command = db.DataSource.CreateCommand(sql);
        for (var i = 0; i < values.Length; i++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "p" + i;
            parameter.Value = values[i] ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return await command.ExecuteScalarAsync();
    }

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
