using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveMaterializerTests
{
    [Theory(DisplayName = "[UC-006][SEC-112-01/02] real public buffered and streamed partial outputs refuse a save before any write or transaction")]
    [InlineData("buffered-mask")] [InlineData("project")] [InlineData("projection")]
    [InlineData("stream-mask")] [InlineData("stream-project")] [InlineData("stream-projection")]
    public async Task Public_partial_paths_cannot_be_saved(string mode)
    {
        await using var db = await SaveProviderDatabase.OpenAsync("sqlite");
        await SaveMaterializerAssertions.SeedAsync(db);
        var query = SaveMaterializerAssertions.Query(db);
        SaveProbeNode partial;
        switch (mode)
        {
            case "buffered-mask": partial = Assert.Single(await query.Redact(n => n.ParentId).ToListAsync()); break;
            case "project": partial = Assert.Single(await query.ProjectAsync([n => n.Id, n => n.Name],
                SaveMaterializerAssertions.Projected)); break;
            case "projection": partial = Assert.Single(await query.ProjectColumn(n => n.Id).ProjectColumn(n => n.Name)
                .ProjectionAsync(SaveMaterializerAssertions.Projected)); break;
            case "stream-mask": partial = await SaveMaterializerAssertions.StreamOne(query.Redact(n => n.ParentId).StreamAsync()); break;
            case "stream-project": partial = await SaveMaterializerAssertions.StreamOne(query.StreamProjectAsync(
                [n => n.Id, n => n.Name], SaveMaterializerAssertions.Projected)); break;
            default: partial = await SaveMaterializerAssertions.StreamOne(query.ProjectColumn(n => n.Id).ProjectColumn(n => n.Name)
                .StreamProjectionAsync(SaveMaterializerAssertions.Projected)); break;
        }
        Assert.Equal(SaveMaterializerAssertions.RootId, partial.Id);
        await SaveMaterializerAssertions.RefusedAsync(db, partial);
        Assert.Equal("root-origin", await db.ScalarAsync("SELECT name FROM gs112_probe_node WHERE id=@p", partial.Id));
    }

    [Theory(DisplayName = "[UC-006][SEC-112-01/02] complete public reads remain explicit full-value inputs and stream masking is frozen at capture")]
    [InlineData(false)] [InlineData(true)]
    public async Task Full_read_and_pre_mask_stream_snapshot_can_be_saved(bool frozenStream)
    {
        await using var db = await SaveProviderDatabase.OpenAsync("sqlite");
        await SaveMaterializerAssertions.SeedAsync(db);
        var query = SaveMaterializerAssertions.Query(db);
        SaveProbeNode full;
        if (frozenStream)
        {
            var stream = query.StreamAsync();
            query.Redact(n => n.ParentId);
            full = await SaveMaterializerAssertions.StreamOne(stream);
        }
        else full = Assert.Single(await query.ToListAsync());
        full.Name = "explicit complete update";
        db.ClearEvents();
        var result = await global::Neemle.OrmLite.GraphSave.SaveAsync(db.Context,
            new GraphSaveRequest<SaveProbeNode>(full, GraphSaveIntent.Update,
                new(GraphSaveIntents.Empty, GraphSavePermissions.None, SaveProbeModel.Room)));
        Assert.True(result.WasWritten(full)); SaveProviderAssertions.OneTransaction(db, 1, TransactionPhase.Commit);
        Assert.Equal(full.Name, await db.ScalarAsync("SELECT name FROM gs112_probe_node WHERE id=@p", full.Id));
    }

    [Theory(DisplayName = "[UC-006][SEC-112-01/02] real read-graph root and related canonical masked objects retain partial provenance")]
    [InlineData(false)] [InlineData(true)]
    public async Task Graph_root_and_related_materializers_are_fenced(bool related)
    {
        await using var db = await SaveProviderDatabase.OpenAsync("sqlite");
        await SaveMaterializerAssertions.SeedAsync(db);
        SaveReadGraphModel.Register();
        var query = new Query<SaveProbeNode>(db.Context, SaveReadGraphModel.Map)
            .Where(n => n.Id == SaveMaterializerAssertions.RootId).Redact(n => n.Name);
        var roots = await query.ToGraphListAsync(new GraphRequest<SaveProbeNode>(new(new(3, 5, 5), new(5, 10, 5)),
            SaveReadGraphModel.Children));
        var root = Assert.Single(roots);
        var child = Assert.Single(root.Children);
        var target = related ? child : root;
        Assert.Equal("", target.Name);
        await SaveMaterializerAssertions.RefusedAsync(db, target);
        Assert.Equal(related ? "child-origin" : "root-origin",
            await db.ScalarAsync("SELECT name FROM gs112_probe_node WHERE id=@p", target.Id));
    }
}
