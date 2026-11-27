namespace Neemle.OrmLite;

public sealed record OrphanReplacementRequest<TChild, TChildKey>
    where TChild : class where TChildKey : notnull
{
    public OrphanReplacementRequest(IReadOnlyList<TChildKey> DesiredKeys, OrphanPresentWrites<TChild> PresentWrites)
    {
        if (DesiredKeys is null || PresentWrites is null || DesiredKeys.Any(k => k is null))
            throw OrphanRefusal.At(OrphanError.InvalidRequest);
        this.DesiredKeys = Array.AsReadOnly(DesiredKeys.ToArray());
        this.PresentWrites = PresentWrites;
    }

    public IReadOnlyList<TChildKey> DesiredKeys { get; }
    public OrphanPresentWrites<TChild> PresentWrites { get; }
}
