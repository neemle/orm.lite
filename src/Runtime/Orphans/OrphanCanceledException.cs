namespace Neemle.OrmLite;

public sealed class OrphanCanceledException : OperationCanceledException
{
    internal OrphanCanceledException(OrphanOutcome outcome, CancellationToken token, Exception? cause = null)
        : base($"Orphan replacement canceled ({outcome}).", cause, token)
    {
        if (outcome is not (OrphanOutcome.NotStarted or OrphanOutcome.RolledBack))
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        Outcome = outcome;
    }

    public OrphanOutcome Outcome { get; }
    public override string ToString() => $"{nameof(OrphanCanceledException)}: {Message}";
}
