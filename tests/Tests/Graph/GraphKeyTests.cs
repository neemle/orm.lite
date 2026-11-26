using System.Data.Common;
using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Tests.Graph;

[Collection("Graph")]
public sealed class GraphKeyTests
{
    [Fact(DisplayName = "[AC-108-03/06] typed string, Guid, long and enum link keys preserve equality in finite cycles")]
    public async Task Link_storage_values_are_read_as_the_registered_key_type()
    {
        await using var db = await GraphDatabase.OpenAsync();
        await VerifyAsync(db.Context, "text", ColumnType.Text, new[] { "X", "x" });
        await VerifyAsync(db.Context, "guid", ColumnType.Uuid, new[] { Guid.NewGuid(), Guid.NewGuid() });
        await VerifyAsync(db.Context, "long", ColumnType.Int64, new[] { long.MaxValue - 1, long.MaxValue });
        await VerifyAsync(db.Context, "enum", ColumnType.Text, new[] { GraphKeyKind.First, GraphKeyKind.Second });
    }

    private static async Task VerifyAsync<TKey>(OrmContext db, string suffix, ColumnType type, TKey[] keys)
        where TKey : notnull
    {
        var table = "graph_keys_" + suffix;
        var descriptor = EntityDescriptor.Of("TypedKeyRow", table, false).Property("Id", "id", type, false, true);
        Func<DbDataReader, GraphTypedKeyRow<TKey>> map = r => new()
            { Id = ScalarValue.To<TKey>(r.GetValue(r.GetOrdinal("id")), table, "id") };
        OrmMeta.Register<GraphTypedKeyRow<TKey>>(descriptor);
        GraphModel.Register(new GraphEntityBinding<GraphTypedKeyRow<TKey>>(descriptor, map,
            new GraphKeyBinding<GraphTypedKeyRow<TKey>, TKey>("id", row => row.Id),
            new GraphLinkBinding<GraphTypedKeyRow<TKey>, GraphTypedKeyRow<TKey>, TKey, TKey>("Peers",
                new(table + "_links", "from_id", "to_id"), row => row.Id, (row, peers) => row.Peers = peers)));
        await SeedAsync(db, table, keys);
        var edge = GraphPaths.Edge<GraphTypedKeyRow<TKey>, GraphTypedKeyRow<TKey>>("Peers");
        var roots = await new Query<GraphTypedKeyRow<TKey>>(db, map).ToGraphListAsync(
            new GraphRequest<GraphTypedKeyRow<TKey>>(GraphDomain.Limits(), edge.Then(edge)));
        Assert.Equal(2, roots.Count);
        var first = roots.Single(r => EqualityComparer<TKey>.Default.Equals(r.Id, keys[0]));
        var second = roots.Single(r => EqualityComparer<TKey>.Default.Equals(r.Id, keys[1]));
        Assert.Same(second, Assert.Single(first.Peers));
        Assert.Same(first, Assert.Single(second.Peers));
        Assert.Same(first, first.Peers[0].Peers[0]);
    }

    private static async Task SeedAsync<TKey>(OrmContext db, string table, TKey[] keys) where TKey : notnull
    {
        await using var connection = await db.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        var storage = typeof(TKey) == typeof(long) ? "BIGINT" : "TEXT";
        command.CommandText = $"CREATE TABLE {table} (id {storage} PRIMARY KEY); "
            + $"CREATE TABLE {table}_links (from_id {storage}, to_id {storage}); "
            + $"INSERT INTO {table} VALUES (@first), (@second); "
            + $"INSERT INTO {table}_links VALUES (@first, @second), (@second, @first);";
        command.AddParam("first", keys[0]);
        command.AddParam("second", keys[1]);
        await command.ExecuteNonQueryAsync();
    }
}
