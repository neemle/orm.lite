namespace Neemle.OrmLite;

internal static class PolicyPlainReader
{
    internal static async Task<List<T>> ReadAsync<T>(GraphQueryState<T> state, CancellationToken ct) where T : class
    {
        if (state.Unsupported || !GraphModel.Find(typeof(T)).Maps(state.Map))
            throw new GraphRequestException(GraphRequestError.UnsupportedShape);
        if (state.Connection is { } connection) return await ReadBoundAsync(state, connection, ct);
        await using var owned = await state.Database.DataSource.OpenConnectionAsync(ct);
        return await ReadBoundAsync(state, owned, ct);
    }

    private static async Task<List<T>> ReadBoundAsync<T>(GraphQueryState<T> state,
        System.Data.Common.DbConnection connection, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var statement = StatementCompiler.Compile(state.Root, state.Database.Provider, StatementKind.Read);
        await using var command = StatementCommand.Create(state.Database.DataSource, connection,
            state.Transaction, statement);
        foreach (var parameter in statement.Parameters)
            command.AddParam(parameter.Name, parameter.Value ?? DBNull.Value);
        await using var reader = await command.RunReaderAsync(StatementKind.Read, ct);
        var rows = new List<T>();
        var map = OneToOneReadMap.Wrap(state.Map, state.Masks.Count != 0);
        while (await reader.ReadAsync(ct)) rows.Add(map(reader));
        return rows;
    }
}