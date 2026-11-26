using System.Data.Common;
using static Neemle.OrmLite.Tests.Graph.Security.SecurityModel;

namespace Neemle.OrmLite.Tests.Graph.Security;

internal static class SecuritySeed
{
    internal static async Task CreateAsync(DbDataSource source)
    {
        await using var command = source.CreateCommand();
        command.CommandText = $"CREATE TABLE {Quote(RootTable)} ({Quote(KeyColumn)} TEXT PRIMARY KEY, "
            + $"{Quote(PayloadColumn)} TEXT NOT NULL); CREATE TABLE {Quote(LeafTable)} "
            + $"({Quote(KeyColumn)} TEXT PRIMARY KEY,{Quote(ParentColumn)} TEXT NULL, "
            + $"{Quote(PayloadColumn)} TEXT NOT NULL); CREATE TABLE {Quote(LinkTable)} "
            + $"({Quote(LinkRoot)} TEXT,{Quote(LinkLeaf)} TEXT); "
            + "CREATE TABLE sec108_witness(marker TEXT); INSERT INTO sec108_witness VALUES ('alive');";
        await command.ExecuteNonQueryAsync();
        await RowsAsync(source);
    }

    private static async Task RowsAsync(DbDataSource source)
    {
        await using var command = source.CreateCommand();
        command.CommandText = $"INSERT INTO {Quote(RootTable)} VALUES (@a,@ra),(@b,@rb),('empty','empty'); "
            + $"INSERT INTO {Quote(LeafTable)} VALUES (@la,@a,@lr),(@lb,@b,@lr),('missing','missing',@lr),"
            + $"('nullable',NULL,@lr); INSERT INTO {Quote(LinkTable)} VALUES (@a,@lb),(@b,@la);";
        command.AddParam("a", KeyA);
        command.AddParam("b", KeyB);
        command.AddParam("ra", RowA);
        command.AddParam("rb", RowB);
        command.AddParam("la", LeafA);
        command.AddParam("lb", LeafB);
        command.AddParam("lr", LeafRow);
        await command.ExecuteNonQueryAsync();
    }
}
