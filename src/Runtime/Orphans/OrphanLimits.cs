namespace Neemle.OrmLite;

public sealed record OrphanLimits
{
    public OrphanLimits(int MaxObserved, int MaxDesired, int MaxCandidates, int MaxWrites, int MaxStatements)
    {
        if (MaxObserved <= 0 || MaxDesired <= 0 || MaxCandidates <= 0 || MaxWrites <= 0 || MaxStatements <= 0)
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        this.MaxObserved = MaxObserved;
        this.MaxDesired = MaxDesired;
        this.MaxCandidates = MaxCandidates;
        this.MaxWrites = MaxWrites;
        this.MaxStatements = MaxStatements;
    }

    public int MaxObserved { get; }
    public int MaxDesired { get; }
    public int MaxCandidates { get; }
    public int MaxWrites { get; }
    public int MaxStatements { get; }
}
