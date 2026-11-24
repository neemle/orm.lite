using Neemle.OrmLite.Tests.SoftDelete.Fixtures;
using Xunit;

namespace Neemle.OrmLite.Tests.SoftDelete.Writes;

public abstract class SoftWriteProbe : IAsyncLifetime
{
    protected SoftDeleteDb Db { get; } = new();

    public Task InitializeAsync() => Db.InitializeAsync();

    public Task DisposeAsync() => Db.DisposeAsync();

    protected async Task<object?> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = Db.Connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        var scalar = await command.ExecuteScalarAsync();
        return scalar is DBNull ? null : scalar;
    }

    protected async Task<string?> DeletedAtOfAsync(Guid id) =>
        (string?)await ScalarAsync("SELECT deleted_at FROM soft_roots WHERE id = $id", ("$id", id));

    protected async Task<long> RankOfAsync(Guid id) =>
        (long)(await ScalarAsync("SELECT rank FROM soft_roots WHERE id = $id", ("$id", id)))!;

    protected async Task<long> RowCountAsync() =>
        (long)(await ScalarAsync("SELECT COUNT(*) FROM soft_roots"))!;
}
