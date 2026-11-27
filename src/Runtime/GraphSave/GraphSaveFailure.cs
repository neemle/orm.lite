namespace Neemle.OrmLite;

internal static class GraphSaveFailure
{
    internal static Exception Wrap(Exception primary, GraphSaveOutcome outcome, GraphSaveResult? result = null)
    {
        if (primary is OperationCanceledException canceled
            && outcome is GraphSaveOutcome.NotStarted or GraphSaveOutcome.RolledBack)
            return new GraphSaveCanceledException(outcome, primary, canceled.CancellationToken);
        var error = outcome switch
        {
            GraphSaveOutcome.Committed => GraphSaveError.PostCommitCleanup,
            GraphSaveOutcome.Unknown => primary is GraphSaveException { Error: GraphSaveError.RollbackUnconfirmed }
                ? GraphSaveError.RollbackUnconfirmed : GraphSaveError.CommitUnknown,
            _ => primary is GraphSaveException typed ? typed.Error : GraphSaveError.ProviderFailure,
        };
        return new GraphSaveException(error, outcome, primary, outcome == GraphSaveOutcome.Committed ? result : null);
    }
}
