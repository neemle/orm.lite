using System.Data.Common;
using Neemle.OrmLite.Sqlite;

namespace Neemle.OrmLite.Tests.Maintenance.Execution;

// A maintenance provider through the public port whose every answer a test scripts: the gate, contention, the lock
// guard, the retry slice and the statement texts. It records each call it gets in the rig's log.
internal sealed class ScriptedProvider(EffectLog log) : ISqlProvider, IMaintenanceProvider
{
    internal const string Checkpoint = "CHECKPOINT";
    internal const string Vacuum = "VACUUM";
    internal const string Analyze = "ANALYZE";
    internal const string Reindex = "REINDEX";
    internal const string Integrity = "INTEGRITY";
    internal const string Lookup = "LOOKUP";
    internal const string Set = "SET";
    internal const string Reset = "RESET";

    public string Id => "scripted";

    public CapabilitySet Capabilities { get; set; } = CapabilitySet.Everything();

    public ISerializer CreateSerializer() => SqliteProvider.Instance.CreateSerializer();

    public TimeSpan MaintenanceTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public string? Key { get; set; }

    public bool HasWriterGate { get; set; } = true;

    public TimeSpan? ContentionRetrySlice { get; set; }

    // The lock guard the provider renders, or none.
    internal bool Guarded { get; set; }

    // Whether REINDEX of an index first looks its schema up.
    internal bool LooksUp { get; set; }

    // The gate: by default entered at once, with a release the log records.
    internal Func<TimeSpan, CancellationToken, ValueTask<MaintenanceWriteLease>>? Gate { get; set; }

    // Runs inside IsContention, after the call is logged; by default contention is what ScriptedDbException says it is.
    internal Func<DbException, bool>? Classify { get; set; }

    // Runs inside every Render*, before it answers: the step's preparation.
    internal Action? OnRender { get; set; }

    // Runs inside ReadIntegrityFinding, before it answers: the reducer's Accept.
    internal Action? OnFinding { get; set; }

    // Runs inside IntegrityEngineCap, before it answers: the reducer's Complete.
    internal Action? OnCap { get; set; }

    // Runs inside the gate lease's release, before it is logged done.
    internal Func<Task>? OnRelease { get; set; }

    internal List<TimeSpan> GateTimeouts { get; } = [];

    internal List<CancellationToken> GateTokens { get; } = [];

    internal int Entries { get; private set; }

    internal List<TimeSpan> GuardRenders { get; } = [];

    internal int Classifications { get; private set; }

    // How many rows the integrity reducer handed over.
    internal int Findings { get; private set; }

    public string? WriterKeyOf(DbDataSource source) => Key;

    public ValueTask<MaintenanceWriteLease> TryEnterWriteAsync(
        DbDataSource source, TimeSpan timeout, CancellationToken ct)
    {
        log.Add("gate");
        GateTimeouts.Add(timeout);
        GateTokens.Add(ct);
        return Gate is { } gate ? gate(timeout, ct) : ValueTask.FromResult(Entered());
    }

    internal MaintenanceWriteLease Entered()
    {
        Entries++;
        return new(true, new Release(this, log));
    }

    public bool IsContention(DbException failure)
    {
        Classifications++;
        log.Add("classify");
        return Classify is { } classify ? classify(failure) : failure is ScriptedDbException { Contention: true };
    }

    public MaintenanceSessionGuard? RenderLockGuard(TimeSpan remaining)
    {
        GuardRenders.Add(remaining);
        return Guarded ? new MaintenanceSessionGuard(Set, Reset) : null;
    }

    public string RenderWalCheckpoint(WalCheckpointMode mode) => Rendered(Checkpoint);

    public string RenderVacuum(VacuumMode mode, EntityDescriptor? table) => Rendered(Vacuum);

    public string RenderAnalyze(AnalyzeScope scope, EntityDescriptor? table) => Rendered(Analyze);

    public string RenderReindex(
        ReindexScope scope, ReindexMode mode, EntityDescriptor? table, IndexDescriptor? index, string? indexSchema) =>
        Rendered(indexSchema is null ? Reindex : Reindex + " " + indexSchema);

    public string? RenderIndexSchemaLookup(EntityDescriptor table) => LooksUp ? Lookup : null;

    public string RenderIntegrityCheck(IntegrityCheckMode mode, EntityDescriptor? table, int maxFindings) =>
        Rendered(Integrity);

    public IntegrityFinding? ReadIntegrityFinding(IntegrityCheckMode mode, DbDataReader row)
    {
        Findings++;
        OnFinding?.Invoke();
        return row.GetString(0) == "ok" ? null : new IntegrityFinding(mode, row.GetString(0));
    }

    public int? IntegrityEngineCap(IntegrityCheckMode mode, EntityDescriptor? table)
    {
        OnCap?.Invoke();
        return null;
    }

    private string Rendered(string text)
    {
        log.Add("render " + text);
        OnRender?.Invoke();
        return text;
    }

    private sealed class Release(ScriptedProvider provider, EffectLog log) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (provider.OnRelease is { } hook) await hook();
            log.Add("release gate");
        }
    }
}
