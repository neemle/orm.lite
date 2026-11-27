namespace Neemle.OrmLite;

public sealed class OrphanException : Exception
{
    internal OrphanException(OrphanError error, OrphanOutcome outcome, Exception? cause = null,
        OrphanResult? committedResult = null) : base($"Orphan replacement: {error} ({outcome}).", cause)
    {
        Error = error;
        Outcome = outcome;
        CommittedResult = committedResult;
    }

    public OrphanError Error { get; }
    public OrphanOutcome Outcome { get; }
    public OrphanResult? CommittedResult { get; }
    public override string ToString() => $"{nameof(OrphanException)}: {Message}";
}
