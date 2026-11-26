using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveCancellationTests
{
    [Theory(DisplayName = "[UC-006][AC-112-03/09] preflight and successful-prefix cancellation preserve exact known rollback truth")]
    [InlineData(false)] [InlineData(true)]
    public async Task Cancellation_before_commit_uses_uncancelled_rollback(bool afterPrefix)
    {
        var probe = new SaveFaultProbe("cancel-prefix");
        var request = SaveFaultAssertions.Request(out var root, out var child);
        await using var source = new SaveFaultSource(probe);
        await using var context = new OrmContext(source, new SaveFaultProvider(probe));
        if (!afterPrefix) probe.Cancellation.Cancel();
        var error = await Assert.ThrowsAsync<GraphSaveCanceledException>(() =>
            global::Neemle.OrmLite.GraphSave.SaveAsync(context, request, probe.Cancellation.Token));
        Assert.Equal(afterPrefix ? GraphSaveOutcome.RolledBack : GraphSaveOutcome.NotStarted, error.Outcome);
        Assert.Equal(afterPrefix ? 1 : 0, probe.Executions);
        Assert.Equal(afterPrefix ? 1 : 0, probe.Rollbacks);
        Assert.Equal(0, probe.Commits); Assert.Equal(0, probe.Writes);
        Assert.False(probe.RollbackTokenCanCancel); Assert.Null(child.ParentId); Assert.Single(root.Children);
        if (afterPrefix) SaveFaultAssertions.Cleaned(probe); else Assert.Equal(0, probe.Opens);
    }

    [Fact(DisplayName = "[UC-006][AC-112-09] cancellation racing after commit starts cannot turn acknowledged commit into rollback")]
    public async Task Acknowledged_commit_wins_late_cancellation()
    {
        var probe = new SaveFaultProbe("late-cancel");
        var request = SaveFaultAssertions.Request(out var root, out var child);
        await using var source = new SaveFaultSource(probe);
        await using var context = new OrmContext(source, new SaveFaultProvider(probe));
        var result = await global::Neemle.OrmLite.GraphSave.SaveAsync(context, request, probe.Cancellation.Token);
        Assert.True(probe.Cancellation.IsCancellationRequested);
        Assert.Equal(GraphSaveOutcome.Committed, result.Outcome);
        Assert.Equal(2, probe.CommittedWrites); Assert.Equal(0, probe.Rollbacks); Assert.Equal(1, probe.Commits);
        Assert.True(result.WasWritten(root)); Assert.Equal(root.Id, result.BoundForeignKey<SaveProbeNode, Guid>(child, "ParentId"));
        Assert.Null(child.ParentId); SaveFaultAssertions.Cleaned(probe);
    }
}
