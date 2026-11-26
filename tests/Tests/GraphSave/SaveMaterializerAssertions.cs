using System.Data.Common;
using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

internal static class SaveMaterializerAssertions
{
    internal static readonly Guid RootId = SaveProbeModel.Id(501);
    internal static readonly Guid ChildId = SaveProbeModel.Id(502);
    internal static async Task SeedAsync(SaveProviderDatabase db)
    {
        await using var connection = await db.Context.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO gs112_probe_node VALUES (@root,NULL,'root-origin');"
            + "INSERT INTO gs112_probe_node VALUES (@child,@root,'child-origin');";
        command.AddParam("root", RootId);
        command.AddParam("child", ChildId);
        await command.ExecuteNonQueryAsync();
    }
    internal static Query<SaveProbeNode> Query(SaveProviderDatabase db) =>
        new Query<SaveProbeNode>(db.Context, SaveProbeModel.MapNode).Where(n => n.Id == RootId);
    internal static SaveProbeNode Projected(DbDataReader r) => new()
    { Id = r.GetGuid(r.GetOrdinal("id")), Name = r.GetString(r.GetOrdinal("name")) };
    internal static async Task<SaveProbeNode> StreamOne(IAsyncEnumerable<SaveProbeNode> stream)
    {
        var result = new List<SaveProbeNode>();
        await foreach (var row in stream) result.Add(row);
        return Assert.Single(result);
    }
    internal static async Task RefusedAsync(SaveProviderDatabase db, SaveProbeNode entity)
    {
        db.ClearEvents();
        var request = new GraphSaveRequest<SaveProbeNode>(entity, GraphSaveIntent.Update,
            new(GraphSaveIntents.Empty, GraphSavePermissions.None, SaveProbeModel.Room));
        var error = await Assert.ThrowsAsync<GraphSaveException>(() => global::Neemle.OrmLite.GraphSave.SaveAsync(db.Context, request));
        Assert.Equal(GraphSaveError.UnsupportedMapping, error.Error);
        Assert.Equal(GraphSaveOutcome.NotStarted, error.Outcome);
        Assert.Empty(db.Statements); Assert.Empty(db.Transactions);
        Assert.DoesNotContain("origin", error.ToString());
    }
}
