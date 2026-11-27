using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveOutcomeTests
{
    [Theory(DisplayName = "[UC-006][AC-112-09] unconfirmed rollback and lost commit response never become a false settled outcome")]
    [InlineData("rollback-unknown", GraphSaveError.RollbackUnconfirmed)]
    [InlineData("commit-unknown", GraphSaveError.CommitUnknown)]
    public async Task Uncertain_outcomes_stay_unknown(string mode, GraphSaveError expected)
    {
        var probe = new SaveFaultProbe(mode);
        var request = SaveFaultAssertions.Request(out _, out var child);
        await using var source = new SaveFaultSource(probe);
        await using var context = new OrmContext(source, new SaveFaultProvider(probe));
        var error = await Assert.ThrowsAsync<GraphSaveException>(() => global::Neemle.OrmLite.GraphSave.SaveAsync(context, request));
        Assert.Equal(expected, error.Error); Assert.Equal(GraphSaveOutcome.Unknown, error.Outcome);
        Assert.Null(error.CommittedResult); Assert.Null(child.ParentId);
        var retained = SaveFaultAssertions.OriginalErrors(error);
        Assert.Contains(mode == "commit-unknown" ? probe.CommitError : probe.Primary, retained);
        if (mode == "commit-unknown")
        {
            Assert.True(probe.DisposalRollbackAcknowledged); Assert.Equal(0, probe.Writes);
            Assert.Equal(1, probe.Commits); Assert.Equal(0, probe.Rollbacks);
        }
        else { Assert.Contains(probe.RollbackError, retained); Assert.Equal(1, probe.Rollbacks); Assert.Equal(0, probe.Commits); }
        Assert.DoesNotContain("private-", error.ToString()); SaveFaultAssertions.Cleaned(probe);
    }

    [Fact(DisplayName = "[UC-006][AC-112-09] committed release failure retains the prebuilt typed result and forbids blind retry")]
    public async Task Postcommit_failure_has_the_committed_result()
    {
        var probe = new SaveFaultProbe("postcommit");
        var request = SaveFaultAssertions.Request(out var root, out var child);
        await using var source = new SaveFaultSource(probe);
        await using var context = new OrmContext(source, new SaveFaultProvider(probe));
        var error = await Assert.ThrowsAsync<GraphSaveException>(() => global::Neemle.OrmLite.GraphSave.SaveAsync(context, request));
        Assert.Equal(GraphSaveError.PostCommitCleanup, error.Error); Assert.Equal(GraphSaveOutcome.Committed, error.Outcome);
        Assert.Same(probe.ReleaseError, error.InnerException);
        var result = Assert.IsType<GraphSaveResult>(error.CommittedResult);
        Assert.True(result.WasWritten(root)); Assert.True(result.WasWritten(child));
        Assert.Equal(root.Id, result.BoundForeignKey<SaveProbeNode, Guid>(child, "ParentId"));
        Assert.Null(child.ParentId); Assert.Equal(2, probe.CommittedWrites); Assert.Equal(0, probe.Rollbacks);
        SaveFaultAssertions.Cleaned(probe);
    }

    [Fact(DisplayName = "[UC-006][AC-112-09][SEC] primary plus every observed cleanup fault survive all cleanup attempts")]
    public async Task Multiple_cleanup_failures_do_not_replace_the_operation_primary()
    {
        var probe = new SaveFaultProbe("multi-cleanup");
        var request = SaveFaultAssertions.Request(out _, out var child);
        await using var source = new SaveFaultSource(probe);
        await using var context = new OrmContext(source, new SaveFaultProvider(probe));
        var error = await Assert.ThrowsAsync<GraphSaveException>(() => global::Neemle.OrmLite.GraphSave.SaveAsync(context, request));
        Assert.Equal(GraphSaveOutcome.RolledBack, error.Outcome); Assert.Same(probe.Primary, error.InnerException);
        var retained = SaveFaultAssertions.OriginalErrors(error);
        Assert.All(new[] { probe.Primary, probe.CommandError, probe.CloseError, probe.TransactionError,
            probe.ConnectionError, probe.ReleaseError }, e => Assert.Contains(e, retained));
        Assert.Equal(1, probe.Rollbacks); Assert.Equal(0, probe.Commits); Assert.Null(child.ParentId);
        Assert.DoesNotContain("private-", error.ToString()); Assert.Empty(probe.Primary.Data);
        SaveFaultAssertions.Cleaned(probe);
    }
}
