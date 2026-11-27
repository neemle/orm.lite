using Neemle.OrmLite.Tests.Diagnostics;
using Xunit;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveStampPrivacyTests
{
    [Fact(DisplayName = "[UC-006][SEC-112-01/02] native acquisition stamp failure is retained only for trusted inspection with logging enabled")]
    public async Task Actor_stamp_failure_has_safe_routine_output_and_complete_owned_cleanup()
    {
        var probe = new SaveFaultProbe("stamp");
        var provider = new SaveStampProvider(probe);
        var sink = new RecordingSink();
        var diagnostics = OrmDiagnostics.Create(provider, new OrmLiteDiagnosticsOptions
        { Sink = sink, StatementLevel = OrmLogLevel.Debug, TransactionLevel = OrmLogLevel.Debug, LogParameters = true });
        await using var raw = new SaveStampSource(probe);
        await using var gated = new GatedDataSource(provider, raw, raw, raw, diagnostics);
        await using var context = new OrmContext(gated, provider);
        var request = SaveFaultAssertions.Request(out _, out var child);
        var error = await Assert.ThrowsAsync<GraphSaveException>(() => global::Neemle.OrmLite.GraphSave.SaveAsync(context, request));
        Assert.Equal(GraphSaveOutcome.NotStarted, error.Outcome); Assert.Same(probe.Primary, error.InnerException);
        Assert.DoesNotContain("private-operation-sentinel", error.ToString());
        Assert.Empty(sink.Statements); Assert.Empty(sink.Transactions);
        Assert.Equal(1, probe.Opens); Assert.Equal(1, probe.Closes); Assert.Equal(1, probe.ConnectionDisposals);
        Assert.Equal(0, probe.Executions); Assert.Null(probe.Transaction); Assert.Null(child.ParentId);
    }
}
