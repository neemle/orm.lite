using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveFaultTransaction(DbConnection connection, SaveFaultProbe probe) : DbTransaction
{
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => connection;
    public override void Commit() => throw new NotSupportedException();
    public override void Rollback() => throw new NotSupportedException();
    public override Task CommitAsync(CancellationToken ct = default)
    {
        probe.Commits++; probe.Events.Add("commit-start");
        if (probe.Mode == "commit-unknown") return Task.FromException(probe.CommitError);
        if (probe.Mode == "late-cancel") probe.Cancellation.Cancel();
        probe.CommittedWrites = probe.Writes;
        return Task.CompletedTask;
    }
    public override Task RollbackAsync(CancellationToken ct = default)
    {
        probe.Rollbacks++; probe.RollbackTokenCanCancel = ct.CanBeCanceled; probe.Events.Add("rollback");
        if (probe.Mode == "rollback-unknown") return Task.FromException(probe.RollbackError);
        probe.Writes = 0;
        return Task.CompletedTask;
    }
    public override ValueTask DisposeAsync()
    {
        probe.TransactionDisposals++; probe.Events.Add("transaction-dispose");
        if (probe.Mode == "commit-unknown")
        {
            probe.Writes = 0; probe.DisposalRollbackAcknowledged = true; probe.Events.Add("dispose-rollback");
        }
        return probe.Mode == "multi-cleanup" ? ValueTask.FromException(probe.TransactionError) : ValueTask.CompletedTask;
    }
}
