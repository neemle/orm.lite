namespace Neemle.OrmLite;

public interface IOrphanGuardLease : IAsyncDisposable
{
    IReadOnlyList<object> OriginalKeys { get; }
    int StatementsUsed { get; }
    Task VerifyEffectsAsync(OrphanMode mode, CancellationToken ct);
}
