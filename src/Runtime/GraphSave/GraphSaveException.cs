namespace Neemle.OrmLite;

public sealed class GraphSaveException : Exception
{
    internal GraphSaveException(GraphSaveError error, GraphSaveOutcome outcome, Exception? cause = null,
        GraphSaveResult? committedResult = null) : base("Graph save: " + error + " (" + outcome + ").", cause)
    { Error = error; Outcome = outcome; CommittedResult = committedResult; }

    public GraphSaveError Error { get; }
    public GraphSaveOutcome Outcome { get; }
    public GraphSaveResult? CommittedResult { get; }
    public override string ToString() => nameof(GraphSaveException) + ": " + Message;
}
