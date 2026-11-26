namespace Neemle.OrmLite.Tests.GraphSave;

internal sealed class SaveFaultProbe(string mode)
{
    internal string Mode { get; } = mode;
    internal CancellationTokenSource Cancellation { get; } = new();
    internal Exception Primary { get; } = new InvalidOperationException("private-operation-sentinel");
    internal Exception RollbackError { get; } = new InvalidOperationException("private-rollback-sentinel");
    internal Exception CommitError { get; } = new InvalidOperationException("private-commit-sentinel");
    internal Exception ReleaseError { get; } = new InvalidOperationException("private-release-sentinel");
    internal Exception CommandError { get; } = new InvalidOperationException("private-command-dispose");
    internal Exception CloseError { get; } = new InvalidOperationException("private-close");
    internal Exception ConnectionError { get; } = new InvalidOperationException("private-connection-dispose");
    internal Exception TransactionError { get; } = new InvalidOperationException("private-transaction-dispose");
    internal List<string> Events { get; } = [];
    internal SaveFaultConnection? Connection { get; set; }
    internal SaveFaultTransaction? Transaction { get; set; }
    internal int Opens, Executions, Writes, Commits, Rollbacks, Releases, CommandDisposals, ConnectionDisposals;
    internal int TransactionDisposals, Closes, CommittedWrites;
    internal bool RollbackTokenCanCancel, DisposalRollbackAcknowledged;
}
