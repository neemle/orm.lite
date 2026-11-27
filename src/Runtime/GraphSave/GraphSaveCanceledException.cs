namespace Neemle.OrmLite;

public sealed class GraphSaveCanceledException : OperationCanceledException
{
    internal GraphSaveCanceledException(GraphSaveOutcome outcome, Exception cause, CancellationToken token)
        : base("Graph save cancelled (" + outcome + ").", cause, token) => Outcome = outcome;

    public GraphSaveOutcome Outcome { get; }
    public override string ToString() => nameof(GraphSaveCanceledException) + ": " + Message;
}
