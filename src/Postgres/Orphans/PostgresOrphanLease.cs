namespace Neemle.OrmLite.Postgres;

internal sealed class PostgresOrphanLease(OrphanGuardSession session, PostgresOrphanCatalog catalog,
    IReadOnlyList<object> keys) : IOrphanGuardLease
{
    public IReadOnlyList<object> OriginalKeys { get; } = Array.AsReadOnly(keys.ToArray());
    public int StatementsUsed => session.StatementsUsed;

    public async Task VerifyEffectsAsync(OrphanMode mode, CancellationToken ct)
    {
        try
        {
            session.Active();
            session.Shape.Mode(mode);
            await catalog.VerifyAsync(mode, ct);
        }
        catch (Exception error) { throw OrphanGuardFailure.Safe(error, ct); }
    }

    public ValueTask DisposeAsync()
    {
        session.End();
        return ValueTask.CompletedTask;
    }
}
