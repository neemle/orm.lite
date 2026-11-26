using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveSqliteRollbackTests
{
    [Theory(DisplayName = "[UC-006][AC-112-03/06/08/09][SEC] real SQLite failures after successful writes roll back the entire graph")]
    [InlineData(false)] [InlineData(true)]
    public async Task Constraint_after_successful_prefix_leaves_no_subset(bool linkFailure)
    {
        await using var db = await SaveSqliteDatabase.OpenAsync(linkFailure);
        var root = SaveProbeModel.Node(201);
        var child = SaveProbeModel.Node(202);
        if (!linkFailure) child.Name = "reject";
        var tag = new SaveProbeTag { Id = SaveProbeModel.Id(203) };
        root.Children.Add(child); root.Tags.Add(tag);
        var error = await Assert.ThrowsAsync<GraphSaveException>(() =>
            global::Neemle.OrmLite.GraphSave.SaveAsync(db.Context, SaveSqliteAssertions.Request(root, child, tag)));
        Assert.Equal(GraphSaveError.ProviderFailure, error.Error);
        Assert.Equal(GraphSaveOutcome.RolledBack, error.Outcome);
        Assert.Null(error.CommittedResult);
        Assert.NotNull(error.InnerException);
        Assert.DoesNotContain("reject", error.ToString());
        Assert.Null(child.ParentId);
        Assert.Contains(db.Statements.Take(db.Statements.Count - 1), s => s.RowsAffected == 1 && s.Error is null);
        Assert.NotNull(db.Statements.Last().Error);
        Assert.DoesNotContain("SQLite", db.Statements.Last().Error!.ToString());
        SaveSqliteAssertions.OneTransaction(db, db.Statements.Count, TransactionPhase.Rollback);
        Assert.Equal(0L, await db.ScalarAsync("SELECT COUNT(*) FROM gs112_probe_node"));
        Assert.Equal(0L, await db.ScalarAsync("SELECT COUNT(*) FROM gs112_probe_tag"));
        Assert.Equal(0L, await db.ScalarAsync("SELECT COUNT(*) FROM gs112_probe_links"));
    }

    [Theory(DisplayName = "[UC-006][AC-112-04/08] missing update and unchanged anchor refuse without upsert")]
    [InlineData(false)] [InlineData(true)]
    public async Task Missing_target_is_not_inserted(bool anchor)
    {
        await using var db = await SaveSqliteDatabase.OpenAsync();
        var root = SaveProbeModel.Node(301);
        var child = SaveProbeModel.Node(302);
        child.ParentId = root.Id;
        root.Children.Add(child);
        var request = anchor
            ? new GraphSaveRequest<SaveProbeNode>(root, GraphSaveIntent.Insert,
                new(GraphSaveIntents.Empty.With(child, GraphSaveIntent.Unchanged),
                    GraphSavePermissions.None.Allow(SaveProbeModel.Children, new(GraphSaveActions.None)), SaveProbeModel.Room))
            : new(root, GraphSaveIntent.Update, new(GraphSaveIntents.Empty, GraphSavePermissions.None, SaveProbeModel.Room));
        var error = await Assert.ThrowsAsync<GraphSaveException>(() => global::Neemle.OrmLite.GraphSave.SaveAsync(db.Context, request));
        Assert.Equal(anchor ? GraphSaveError.MissingAnchor : GraphSaveError.MissingExisting, error.Error);
        Assert.Equal(GraphSaveOutcome.RolledBack, error.Outcome);
        SaveSqliteAssertions.OneTransaction(db, 1, TransactionPhase.Rollback);
        Assert.Equal(0L, await db.ScalarAsync("SELECT COUNT(*) FROM gs112_probe_node"));
    }
}
