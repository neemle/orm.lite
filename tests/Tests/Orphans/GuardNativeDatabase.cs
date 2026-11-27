using System.Data.Common;
using Neemle.OrmLite.Tests.ForeignKeys;

namespace Neemle.OrmLite.Tests.Orphans;

internal sealed class GuardNativeDatabase(ForeignKeyDatabase database) : IAsyncDisposable
{
    internal OrmContext Context => database.Context;
    internal IOrphanSnapshotGuard Guard => (IOrphanSnapshotGuard)Context.Provider;

    internal static async Task<GuardNativeDatabase> Open(string provider)
    {
        var underlying = provider == "postgres"
            ? await ForeignKeyDatabase.PostgresAsync() : await ForeignKeyDatabase.SqliteAsync();
        var result = new GuardNativeDatabase(underlying);
        try
        {
            var schema = OrmSchema.Of("guard_native", GuardNativeModel.Parent, GuardNativeModel.Child);
            foreach (var sql in DdlCompiler.Render(schema.Baseline(), result.Context.Provider))
                await underlying.ExecuteAsync(sql);
            for (var id = 1; id <= 3; id++)
                await underlying.ExecuteAsync("INSERT INTO guard_parents(id,name) VALUES(@p0,@p1)",
                    GuardNativeModel.Key(id), "parent-" + id);
            await underlying.ExecuteAsync("INSERT INTO guard_children(id,parent_id,name) VALUES"
                + "(@p0,@p1,'first'),(@p2,@p1,'second'),(@p3,@p4,'sibling'),(@p5,NULL,'unowned')",
                GuardNativeModel.Key(11), GuardNativeModel.Key(1), GuardNativeModel.Key(12),
                GuardNativeModel.Key(21), GuardNativeModel.Key(2), GuardNativeModel.Key(31));
            return result;
        }
        catch { await result.DisposeAsync(); throw; }
    }

    internal static DbCommand Command(DbConnection connection, DbTransaction transaction, string sql,
        params object?[] values)
    {
        var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        for (var i = 0; i < values.Length; i++) command.AddParam("p" + i, values[i]);
        return command;
    }

    internal async Task<IReadOnlyList<(Guid Id, Guid? ParentId, string Name)>> Rows(
        DbConnection? connection = null, DbTransaction? transaction = null)
    {
        var owned = connection is null;
        connection ??= await Context.DataSource.OpenConnectionAsync();
        try
        {
            await using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "SELECT id,parent_id,name FROM guard_children ORDER BY id";
            await using var reader = await command.ExecuteReaderAsync();
            var result = new List<(Guid, Guid?, string)>();
            while (await reader.ReadAsync()) result.Add((reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetString(2)));
            return result;
        }
        finally { if (owned) await connection.DisposeAsync(); }
    }

    public ValueTask DisposeAsync() => database.DisposeAsync();
}
