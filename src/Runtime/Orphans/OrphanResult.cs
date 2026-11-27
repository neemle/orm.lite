namespace Neemle.OrmLite;

public sealed class OrphanResult
{
    internal OrphanResult(OrphanOutcome outcome, bool disabled, OrphanCounts counts)
    {
        if (counts is null || !counts.IsValid) throw OrphanRefusal.At(OrphanError.InvalidRequest);
        if (disabled ? outcome != OrphanOutcome.NotStarted || !counts.IsEmpty : outcome != OrphanOutcome.Committed)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        Outcome = outcome;
        Disabled = disabled;
        Nullified = counts.Nullified;
        Deleted = counts.Deleted;
        SoftDeleted = counts.SoftDeleted;
        Inserted = counts.Inserted;
        Updated = counts.Updated;
    }

    public OrphanOutcome Outcome { get; }
    public bool Disabled { get; }
    public int Nullified { get; }
    public int Deleted { get; }
    public int SoftDeleted { get; }
    public int Inserted { get; }
    public int Updated { get; }
}
