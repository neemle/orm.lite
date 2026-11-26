using System.Data.Common;
using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace FaultProof;

internal sealed class FaultSession(FaultControl control, ISqlProvider provider) : ISchemaDiffApplySession
{
    private int _calls;
    public async Task<SchemaDiffObservation> GuardAndInspectAsync(DbTransaction transaction, CancellationToken ct)
    {
        _calls++;
        if (_calls == 2) await FaultAsync();
        if (_calls == 3 && control.Kind == FaultKind.Cancel) control.Cancellation.Cancel();
        return control.Observe((FaultTransaction)transaction);
    }
    private async Task FaultAsync()
    {
        if (control.Kind is FaultKind.Body or FaultKind.Rollback or FaultKind.Composite)
            throw new IOException(FaultControl.Canary + " primary");
        if (control.Kind != FaultKind.IgnoredCancel) return;
        control.Entered.TrySetResult(); await control.Release.Task;
        control.Events.Add("ignored-cancel-settled");
    }
    public Task<IReadOnlyList<Statement>> PlanStrictCreateAsync(CreateTable node, DbTransaction transaction,
        CancellationToken ct) => Task.FromResult(SchemaStrictDdl.Compile(node, provider));
    public Task<SchemaRestoration> RestoreAndVerifyAsync(CancellationToken ct)
    {
        control.Events.Add("session-restore");
        if (ct.CanBeCanceled) throw new IOException("cleanup must be independent of caller token");
        if (_calls > 1 && control.Kind is FaultKind.Restore or FaultKind.Composite)
            throw new IOException(FaultControl.Canary + " restoration");
        return Task.FromResult(SchemaRestoration.NotRequired);
    }
    public ValueTask DisposeAsync()
    {
        control.Events.Add("session-dispose");
        return _calls > 1 && control.Kind == FaultKind.Composite
            ? ValueTask.FromException(new IOException(FaultControl.Canary)) : ValueTask.CompletedTask;
    }
}
