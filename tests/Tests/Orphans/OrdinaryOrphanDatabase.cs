using System.Data.Common;
using Neemle.OrmLite.Tests.ForeignKeys;

namespace Neemle.OrmLite.Tests.Orphans;

internal sealed class OrdinaryOrphanDatabase(ForeignKeyDatabase database, bool soft) : IAsyncDisposable
{
    internal OrmContext Context => database.Context;
    internal IOrphanSnapshotGuard Guard => (IOrphanSnapshotGuard)Context.Provider;
    internal EntityDescriptor Child => soft ? OrdinaryOrphanProfiles.Soft : OrdinaryOrphanProfiles.Scalars;

    internal static async Task<OrdinaryOrphanDatabase> Open(string provider, bool soft)
    {
        var storage = provider == "postgres"
            ? await ForeignKeyDatabase.PostgresAsync() : await ForeignKeyDatabase.SqliteAsync();
        var result = new OrdinaryOrphanDatabase(storage, soft);
        try { await result.Initialize(); return result; }
        catch { await result.DisposeAsync(); throw; }
    }

    private async Task Initialize()
    {
        _ = OrdinaryOrphanProfiles.Request(soft);
        var schema = OrmSchema.Of("ordinary_guard", GuardNativeModel.Parent, Child);
        var nodes = schema.Baseline().Concat(schema.Indexes()).ToList();
        if (soft) nodes.Add(RowHistoryDdl.Install(schema.HistoryTables));
        foreach (var sql in DdlCompiler.Render(nodes, Context.Provider))
            await database.ExecuteAsync(sql);
        await database.ExecuteAsync("INSERT INTO guard_parents(id,name) VALUES(@p0,'ordinary parent')",
            GuardNativeModel.Key(1));
        var insert = soft ? "INSERT INTO ordinary_soft(id,parent_id,name) VALUES(@p0,@p1,'ordinary')"
            : "INSERT INTO ordinary_scalars(id,parent_id) VALUES(@p0,@p1)";
        await database.ExecuteAsync(insert, GuardNativeModel.Key(11), GuardNativeModel.Key(1));
    }

    internal Task<object?> Scalar(string sql) => database.ScalarAsync(sql);

    internal async Task AssertScalars(DbConnection connection, DbTransaction transaction)
    {
        await using var command = GuardNativeDatabase.Command(connection, transaction,
            "SELECT id,parent_id,text,enabled,\"count\",total,number,observed_at FROM ordinary_scalars");
        await using var reader = await command.ExecuteReaderAsync();
        Xunit.Assert.True(await reader.ReadAsync());
        Xunit.Assert.Equal(GuardNativeModel.Key(11), reader.GetGuid(0));
        Xunit.Assert.Equal(GuardNativeModel.Key(1), reader.GetGuid(1));
        Xunit.Assert.Equal("", reader.GetString(2));
        Xunit.Assert.False(reader.GetBoolean(3));
        Xunit.Assert.Equal(0, reader.GetInt32(4));
        Xunit.Assert.Equal(0L, reader.GetInt64(5));
        Xunit.Assert.Equal(0d, reader.GetDouble(6));
        Xunit.Assert.True(reader.IsDBNull(7));
        Xunit.Assert.False(await reader.ReadAsync());
    }

    internal async Task AssertSoft(DbConnection connection, DbTransaction transaction)
    {
        await using var command = GuardNativeDatabase.Command(connection, transaction,
            "SELECT id,parent_id,name,deleted_at FROM ordinary_soft");
        await using var reader = await command.ExecuteReaderAsync();
        Xunit.Assert.True(await reader.ReadAsync());
        Xunit.Assert.Equal(GuardNativeModel.Key(11), reader.GetGuid(0));
        Xunit.Assert.Equal(GuardNativeModel.Key(1), reader.GetGuid(1));
        Xunit.Assert.Equal("ordinary", reader.GetString(2));
        Xunit.Assert.True(reader.IsDBNull(3));
        Xunit.Assert.False(await reader.ReadAsync());
    }

    public ValueTask DisposeAsync() => database.DisposeAsync();
}
