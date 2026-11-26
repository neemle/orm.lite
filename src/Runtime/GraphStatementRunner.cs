using System.Data.Common;

namespace Neemle.OrmLite;

internal static class GraphStatementRunner
{
    internal static async Task<List<T>> ReadAsync<T>(GraphReadContext context, Statement statement,
        Func<DbDataReader, T> map, bool root = false)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        context.Budget.Statement();
        await using var command = StatementCommand.Create(context.Database.DataSource,
            context.Connection, context.Transaction, statement);
        foreach (var parameter in statement.Parameters)
            command.AddParam(parameter.Name, parameter.Value ?? DBNull.Value);
        await using var reader = await command.RunReaderAsync(StatementKind.Read, context.CancellationToken);
        var result = new List<T>();
        while (await reader.ReadAsync(context.CancellationToken))
        {
            context.Budget.Row(root);
            result.Add(map(reader));
        }
        return result;
    }
}
