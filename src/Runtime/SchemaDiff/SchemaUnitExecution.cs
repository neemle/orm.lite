using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class SchemaUnitExecution(SchemaFixtureState state, SchemaFixtureWork work)
{
    internal SchemaFixtureState State => state;
    internal SchemaFixtureWork Work => work;
    internal ISchemaDiffApplySession? Session { get; private set; }
    internal IMigrationUnit? Unit { get; private set; }
    internal DbTransaction? Transaction { get; private set; }
    internal IAsyncDisposable? Release { get; private set; }
    internal SchemaDiffObservation? PostState { get; set; }
    private readonly List<Exception> _cleanup = [];
    private bool _commitStarted;
    private bool _committed;
    private bool _rollbackUnknown;
    private SchemaUnitOutcome _outcome;
    private SchemaRestoration _restoration;

    internal async Task RunAsync(CancellationToken ct)
    {
        state.Budget.Cleanup = false;
        Exception? primary = null;
        try
        {
            Session = await ((ISchemaDiffProvider)state.Plan.Context.Provider)
                .BeginDiffApplyAsync(state.Connection, state.Plan.Inspection, ct);
            Unit = await state.Plan.Planner.BeginUnitAsync(state.Connection, work.Operations, ct);
            var started = await state.Plan.Context.Provider.BeginTransactionAsync(state.Raw,
                state.Plan.Context.DataSource, ct);
            Transaction = started.Transaction; Release = started.Release;
            await SchemaFixtureMutation.RunAsync(this, ct);
            if (work.Kind != SchemaFixtureWorkKind.Inspect)
            {
                ct.ThrowIfCancellationRequested();
                _commitStarted = true;
                await Transaction.CommitAsync(ct); _committed = true; _outcome = SchemaUnitOutcome.Committed;
            }
        }
        catch (Exception error) { primary = error; }
        await CleanupAsync();
        Record();
        var failure = Failure(primary, ct);
        if (failure is not null) throw failure;
    }

    private async Task CleanupAsync()
    {
        state.Budget.Cleanup = true;
        if (Transaction is not null && !_committed) await RollbackAsync();
        await SchemaResourceCleanup.DisposeAsync(Transaction, _cleanup);
        await SchemaResourceCleanup.DisposeAsync(Release, _cleanup);
        await SchemaResourceCleanup.DisposeAsync(Unit, _cleanup);
        if (Session is not null)
        {
            try { _restoration = await Session.RestoreAndVerifyAsync(CancellationToken.None); }
            catch (Exception error) { _restoration = SchemaRestoration.Unknown; _cleanup.Add(error); }
        }
        await SchemaResourceCleanup.DisposeAsync(Session, _cleanup);
    }

    private async Task RollbackAsync()
    {
        try
        {
            await Transaction!.RollbackAsync(CancellationToken.None);
            _outcome = _commitStarted ? SchemaUnitOutcome.Unknown : SchemaUnitOutcome.RolledBack;
        }
        catch (Exception error)
        { _rollbackUnknown = true; _outcome = SchemaUnitOutcome.Unknown; _cleanup.Add(error); }
    }

    private void Record()
    {
        state.Restoration = _restoration;
        if (work.Kind == SchemaFixtureWorkKind.Inspect) return;
        state.Units.Add(new(work.Version, _outcome, _restoration));
        if (work.Kind == SchemaFixtureWorkKind.Bootstrap
            && _outcome is SchemaUnitOutcome.Committed or SchemaUnitOutcome.Unknown) state.Bootstrap = true;
        if (_committed)
        {
            state.Progress = PostState;
            if (work.Receipt is not null) state.Committed.Add(work.Version);
        }
    }

    private Exception? Failure(Exception? primary, CancellationToken ct)
    {
        var code = _commitStarted && !_committed ? SchemaDiffError.CommitUnknown
            : _rollbackUnknown ? SchemaDiffError.RollbackUnknown
            : _restoration == SchemaRestoration.Unknown ? SchemaDiffError.RestorationUnknown
            : SchemaDiffError.CleanupFailure;
        if (primary is null && _cleanup.Count == 0 && _restoration != SchemaRestoration.Unknown) return null;
        state.Outcome = FailureOutcome();
        if (code is SchemaDiffError.CommitUnknown or SchemaDiffError.RollbackUnknown)
            return new SchemaDiffException(code, primary, _cleanup, state.Result());
        if (primary is not null) return SchemaDiffFailures.Wrap(primary, _cleanup, ct, state.Result());
        return new SchemaDiffException(code, cleanup: _cleanup, result: state.Result());
    }
    private SchemaDiffApplyOutcome FailureOutcome()
    {
        if (_outcome == SchemaUnitOutcome.Unknown) return SchemaDiffApplyOutcome.Unknown;
        if (_committed && work.Kind is SchemaFixtureWorkKind.Upgrade or SchemaFixtureWorkKind.Revert)
            return SchemaDiffApplyOutcome.Applied;
        if (_committed && work.Kind == SchemaFixtureWorkKind.Coverage)
            return SchemaDiffApplyOutcome.CoveredByCurrentBaseline;
        return state.Units.Any(u => u.Outcome == SchemaUnitOutcome.Committed)
            ? SchemaDiffApplyOutcome.PartiallyApplied : SchemaDiffApplyOutcome.NotStarted;
    }

}
