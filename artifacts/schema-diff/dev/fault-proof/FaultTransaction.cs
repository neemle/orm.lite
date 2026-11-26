using System.Data;
using System.Data.Common;

namespace FaultProof;

internal sealed class FaultTransaction(FaultControl control, DbConnection connection) : DbTransaction
{
    internal bool Created = control.Created;
    internal bool Applied = control.Applied;
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => connection;
    public override void Commit() => throw new NotSupportedException();
    public override void Rollback() => throw new NotSupportedException();
    public override Task CommitAsync(CancellationToken ct)
    {
        control.Events.Add("commit"); control.Created = Created; control.Applied = Applied;
        if (control.Kind == FaultKind.CommitAfter) throw new IOException(FaultControl.Canary);
        return Task.CompletedTask;
    }
    public override Task RollbackAsync(CancellationToken ct)
    {
        control.Events.Add("rollback");
        if (Created && control.Kind is FaultKind.Rollback or FaultKind.Composite)
            throw new IOException(FaultControl.Canary + " rollback-ack-lost");
        return Task.CompletedTask;
    }
    public override ValueTask DisposeAsync()
    { control.Events.Add("transaction-dispose"); return ValueTask.CompletedTask; }
}
