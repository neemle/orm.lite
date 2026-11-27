using System.Data;
using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class OrphanUnitAcquire(OrphanOperation operation)
{
    private bool _rollbackAttempted;
    private OrphanOutcome _rollbackOutcome;
    internal OrphanOperation Operation { get; } = operation;
    internal DbConnection? Connection { get; set; }
    internal bool OwnsConnection { get; set; }
    internal ProviderTransaction? Started { get; private set; }
    internal bool BeginAttempted { get; private set; }
    internal UnitOfWork? Unit { get; set; }
    internal TransactionTrace? Trace { get; set; }
    internal IOrphanGuardLease? Guard { get; set; }
    internal StreamCleanup Cleanup { get; } = new();
    internal OrphanStatementBudget Budget { get; } = new(operation.Options.Limits.MaxStatements);

    internal async Task StartAsync(CancellationToken ct)
    {
        await OrphanOpen.RunAsync(this, ct);
        ct.ThrowIfCancellationRequested();
        BeginAttempted = true;
        Started = await Operation.Provider.BeginTransactionAsync(Connection!, Operation.Source, ct);
        UnitOfWork.AttachOrphan(this);
        Active();
    }

    internal void Active()
    {
        Operation.Validate();
        if (Unit is null || Unit.Committed || !OwnsConnection || Connection?.State != ConnectionState.Open
            || !ReferenceEquals(Unit.Connection, Connection)
            || !ReferenceEquals(Unit.Transaction.Connection, Connection))
            throw OrphanRefusal.At(OrphanError.ScopeEnded);
        if (Operation.Source is GatedDataSource gated && !GatedDataSource.OriginatesFrom(Connection!, gated))
            throw OrphanRefusal.At(OrphanError.UnsupportedTransaction);
    }

    internal async Task<OrphanOutcome> RollbackAsync(bool uncertainCommit = false)
    {
        if (_rollbackAttempted) return _rollbackOutcome;
        _rollbackAttempted = true;
        _rollbackOutcome = BeginAttempted ? OrphanOutcome.Unknown : OrphanOutcome.NotStarted;
        if (uncertainCommit) AbandonTrace();
        if (Started is { } started)
        {
            var acknowledged = await Cleanup.Attempt(() => new ValueTask(
                started.Transaction.RollbackAsync(CancellationToken.None)));
            if (acknowledged && !uncertainCommit) _rollbackOutcome = OrphanOutcome.RolledBack;
        }
        if (_rollbackOutcome == OrphanOutcome.Unknown) AbandonTrace();
        else EndTrace();
        return _rollbackOutcome;
    }

    internal void AbandonTrace() => CurrentTrace()?.Abandon();

    private TransactionTrace? CurrentTrace() => OwnsConnection
        ? Trace ?? GatedDataSource.LeaseOf(Connection)?.Transaction : null;

    private void EndTrace()
    {
        try { CurrentTrace()?.Dispose(); }
        catch (Exception error) { Cleanup.Add(error); }
    }

    internal async Task CleanAsync()
    {
        if (Guard is { } guard) await Cleanup.Attempt(guard.DisposeAsync);
        Guard = null;
        if (Unit is { } unit)
        {
            var disposed = await Cleanup.Attempt(unit.DisposeAsync);
            if (!disposed && Connection is { } connection)
                disposed = await Cleanup.Attempt(() => new ValueTask(connection.CloseAsync()));
            if (disposed) ReleaseConnectionLease();
        }
        else await UnattachedAsync();
    }

    private async Task UnattachedAsync()
    {
        if (Started is { } started) await Cleanup.Attempt(started.Transaction.DisposeAsync);
        if (OwnsConnection && Connection is { } connection)
        {
            await Cleanup.Attempt(() => new ValueTask(connection.CloseAsync()));
            var disposed = await Cleanup.Attempt(connection.DisposeAsync);
            if (disposed) ReleaseConnectionLease();
        }
        if (Started?.Release is { } release) await Cleanup.Attempt(release.DisposeAsync);
    }

    private void ReleaseConnectionLease()
    {
        try { GatedDataSource.LeaseOf(Connection)?.ReleaseStream(); }
        catch (Exception error) { Cleanup.Add(error); }
    }
}
