namespace Neemle.OrmLite;

public sealed record OrphanOptions
{
    public OrphanOptions(OrphanLimits Limits, OrphanMode Mode = OrphanMode.Disabled,
        OrphanTransactionMode TransactionMode = OrphanTransactionMode.Owned)
    {
        if (Limits is null || !Enum.IsDefined(Mode) || !Enum.IsDefined(TransactionMode))
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        this.Limits = Limits with { };
        this.Mode = Mode;
        this.TransactionMode = TransactionMode;
    }

    public OrphanLimits Limits { get; }
    public OrphanMode Mode { get; }
    public OrphanTransactionMode TransactionMode { get; }
}
