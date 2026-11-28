using Xunit;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// One maintenance facade over scripted doubles: a GatedDataSource over the scripted source (so leases, lanes and
// diagnostics are the library's own), a recording sink, and the manual clock. Nothing here sleeps or reads real time.
internal sealed class ExecutionRig
{
    // A table with one declared index, for REINDEX of an index.
    internal static readonly EntityDescriptor Indexed =
        EntityDescriptor.Of("ExecutionIndexed", "execution_indexed", true).Index("ix_execution", false, false, "v");

    internal ExecutionRig()
    {
        Provider = new ScriptedProvider(Log);
        Source = new ScriptedSource(Log);
        var options = new OrmLiteDiagnosticsOptions { Sink = Sink, StatementLevel = OrmLogLevel.Information };
        var gated = new GatedDataSource(Provider, Source, Source, Source, OrmDiagnostics.Create(Provider, options));
        Maintenance = new OrmMaintenance(new OrmContext(gated, Provider), Clock);
    }

    internal EffectLog Log { get; } = new();

    internal ManualClock Clock { get; } = new();

    internal MaintenanceSink Sink { get; } = new();

    internal ScriptedProvider Provider { get; }

    internal ScriptedSource Source { get; }

    internal OrmMaintenance Maintenance { get; }

    internal CancellationTokenSource Caller { get; } = new();

    internal static TimeSpan Ms(double milliseconds) => TimeSpan.FromMilliseconds(milliseconds);

    // A checkpoint row: busy, log, checkpointed.
    internal static ScriptedReader Row(ScriptedSource source, long busy, long log, long checkpointed) =>
        new(source, [[busy, log, checkpointed]]);

    internal ScriptedReader Rows(params object?[][] rows) => new(Source, rows);

    internal Task<WalCheckpointResult> CheckpointAsync(int timeoutMs = 1000) =>
        Maintenance.CheckpointWalAsync(
            new WalCheckpointRequest(WalCheckpointMode.Truncate) { Timeout = Ms(timeoutMs) }, Caller.Token);

    internal Task<VacuumResult> VacuumAsync(int timeoutMs = 1000) =>
        Maintenance.VacuumAsync(new VacuumRequest(VacuumMode.Plain) { Timeout = Ms(timeoutMs) }, Caller.Token);

    internal Task<AnalyzeResult> AnalyzeAsync(int timeoutMs = 1000) =>
        Maintenance.AnalyzeAsync(new AnalyzeRequest(AnalyzeScope.Database) { Timeout = Ms(timeoutMs) }, Caller.Token);

    internal Task<IntegrityCheckResult> CheckIntegrityAsync(int timeoutMs = 1000) =>
        Maintenance.CheckIntegrityAsync(
            new IntegrityCheckRequest(IntegrityCheckMode.Full) { Timeout = Ms(timeoutMs) }, Caller.Token);

    internal Task<ReindexResult> ReindexIndexAsync(int timeoutMs = 1000) =>
        Maintenance.ReindexAsync(
            new ReindexRequest(ReindexScope.Index, ReindexMode.Blocking)
            {
                Table = Indexed,
                Index = Indexed.Indexes[0],
                Timeout = Ms(timeoutMs),
            },
            Caller.Token);

    // Every connection, command and reader the doubles made was disposed exactly once, and every gate entered was
    // released exactly once.
    internal void AssertUnwound()
    {
        Assert.All(Source.Resources, resource => Assert.Equal(1, resource.Value));
        Assert.Equal(Provider.Entries, Log.Count("release gate"));
    }

    // The call threw OperationCanceledException carrying the caller's token, with `inner` inside.
    internal async Task<OperationCanceledException> CancelledAsync(Task call, Exception? inner)
    {
        var failure = await Record.ExceptionAsync(() => call);
        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(Caller.Token, cancelled.CancellationToken);
        Assert.Same(inner, cancelled.InnerException);
        return cancelled;
    }

    // The checkpoint answers one Completed row unless a test scripts it otherwise.
    internal ExecutionRig WithCompletedCheckpoint()
    {
        Source.Script(ScriptedProvider.Checkpoint, _ => Row(Source, 0, 4, 4));
        return this;
    }
}
