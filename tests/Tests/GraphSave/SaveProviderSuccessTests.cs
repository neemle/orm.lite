using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveProviderSuccessTests
{
    [Theory(DisplayName = "[UC-006][AC-112-01/02/04/05/06/08/09][SEC] real provider graph saves, "
        + "repeats exact pair and preserves disabled rows")]
    [InlineData("sqlite")] [InlineData("postgres")]
    public async Task Success_repeat_and_disabled_navigation_have_exact_database_effects(string provider)
    {
        await using var db = await SaveProviderDatabase.OpenAsync(provider);
        if (provider == "sqlite") Assert.Equal(1L, await db.ScalarAsync("PRAGMA foreign_keys"));
        var root = Graph(out var child, out var tag);
        await Initial(db, root, child, tag);
        await Repeat(db, root, child, tag);
        await Disabled(db, root, child);
    }

    private static SaveProbeNode Graph(out SaveProbeNode child, out SaveProbeTag tag)
    {
        var root = SaveProbeModel.Node(101);
        root.Name = "private ' payload; -- sentinel";
        child = SaveProbeModel.Node(102);
        tag = new SaveProbeTag { Id = SaveProbeModel.Id(103) };
        root.Children.Add(child);
        root.Tags.Add(tag);
        return root;
    }

    private static async Task Initial(SaveProviderDatabase db, SaveProbeNode root,
        SaveProbeNode child, SaveProbeTag tag)
    {
        var result = await global::Neemle.OrmLite.GraphSave.SaveAsync(db.Context,
            SaveProviderAssertions.Request(root, child, tag));
        Assert.Equal(GraphSaveOutcome.Committed, result.Outcome);
        Assert.True(result.WasWritten(root));
        Assert.True(result.WasWritten(child));
        Assert.True(result.WasWritten(tag));
        Assert.Equal(root.Id, result.BoundForeignKey<SaveProbeNode, Guid>(child, "ParentId"));
        Assert.Null(child.ParentId);
        SaveProviderAssertions.OneTransaction(db, 4, TransactionPhase.Commit);
        Assert.Equal(root.Name, await db.ScalarAsync("SELECT name FROM gs112_probe_node WHERE id = @p", root.Id));
        var stored = await db.ScalarAsync("SELECT parent_id FROM gs112_probe_node WHERE id = @p", child.Id);
        Assert.Equal(root.Id, stored is Guid key ? key : Guid.Parse((string)stored!));
        Assert.Equal(2L, await db.ScalarAsync("SELECT COUNT(*) FROM gs112_probe_node"));
        Assert.Equal(1L, await db.ScalarAsync("SELECT COUNT(*) FROM gs112_probe_links"));
    }

    private static async Task Repeat(SaveProviderDatabase db, SaveProbeNode root,
        SaveProbeNode child, SaveProbeTag tag)
    {
        root.Name = "changed root"; child.Name = "changed child";
        db.ClearEvents();
        var result = await global::Neemle.OrmLite.GraphSave.SaveAsync(db.Context,
            SaveProviderAssertions.Request(root, child, tag, true));
        SaveProviderAssertions.OneTransaction(db, 4, TransactionPhase.Commit);
        Assert.False(result.WasWritten(tag));
        Assert.Equal(0, db.Statements.Last().RowsAffected);
        Assert.Equal(2L, await db.ScalarAsync("SELECT COUNT(*) FROM gs112_probe_node"));
        Assert.Equal(1L, await db.ScalarAsync("SELECT COUNT(*) FROM gs112_probe_tag"));
        Assert.Equal(1L, await db.ScalarAsync("SELECT COUNT(*) FROM gs112_probe_links"));
        Assert.Null(child.ParentId);
    }

    private static async Task Disabled(SaveProviderDatabase db, SaveProbeNode root, SaveProbeNode child)
    {
        var reads = root.ChildReads + root.TagReads;
        child.Name = "must not persist";
        root.Name = "root only";
        db.ClearEvents();
        await global::Neemle.OrmLite.GraphSave.SaveAsync(db.Context, new GraphSaveRequest<SaveProbeNode>(root,
            GraphSaveIntent.Update, new(GraphSaveIntents.Empty, GraphSavePermissions.None, SaveProbeModel.Room)));
        SaveProviderAssertions.OneTransaction(db, 1, TransactionPhase.Commit);
        Assert.Equal(reads, root.ChildReads + root.TagReads);
        Assert.Equal("changed child",
            await db.ScalarAsync("SELECT name FROM gs112_probe_node WHERE id = @p", child.Id));
        Assert.Equal(1L, await db.ScalarAsync("SELECT COUNT(*) FROM gs112_probe_links"));
    }
}
