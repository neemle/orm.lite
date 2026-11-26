namespace Neemle.OrmLite;

public static class SchemaDiffFixtureRunner
{
    public static async Task<SchemaDiffApplyResult> RunAsync(OrmContext context, SchemaDiffFixtureRequest request,
        CancellationToken ct = default)
    {
        var state = CreateState(context, request);
        Exception? primary = null; var cleanup = new List<Exception>(); var acquired = false;
        IDisposable? actor = null;
        try
        {
            ct.ThrowIfCancellationRequested();
            state.Raw = await context.DataSource.OpenConnectionAsync(ct);
            state.Connection = new SchemaBudgetConnection(state.Raw, state.Budget);
            actor = DbActor.EnterSystem();
            await context.Provider.StampActorAsync(state.Raw, DbActorValue.System, ct);
            acquired = true;
            foreach (var statement in state.Plan.Run.Acquire)
                await SchemaFixtureCommands.ExecuteAsync(state.Connection, null, statement, ct);
            await RunUnitsAsync(state, ct);
        }
        catch (Exception error) { primary = error; }
        await CleanupAsync(state, acquired, cleanup);
        actor?.Dispose();
        if (primary is not null) throw SchemaDiffFailures.Wrap(primary, cleanup, ct, state.Result());
        if (cleanup.Count > 0) throw new SchemaDiffException(SchemaDiffError.CleanupFailure,
            cleanup: cleanup, result: state.Result());
        return state.Result();
    }

    private static SchemaFixtureState CreateState(OrmContext context, SchemaDiffFixtureRequest request)
    {
        try { return new SchemaFixtureState(SchemaFixturePlan.Create(context, request)); }
        catch (SchemaDiffException) { throw; }
        catch (CapabilityNotSupportedException error) { throw new SchemaDiffException(SchemaDiffError.
            Capability, error); }
        catch (NotSupportedException error) { throw new SchemaDiffException(SchemaDiffError.
            UnsupportedDifference, error); }
        catch (Exception error) { throw new SchemaDiffException(SchemaDiffError.InvalidRequest, error); }
    }

    private static async Task RunUnitsAsync(SchemaFixtureState state, CancellationToken ct)
    {
        var inspect = new SchemaFixtureWork(SchemaFixtureWorkKind.Inspect, state.Plan.Artifact.Version, [], null);
        await new SchemaUnitExecution(state, inspect).RunAsync(ct);
        if (state.Repeat) return;
        foreach (var work in SchemaFixtureSchedule.Build(state)) await new SchemaUnitExecution(state,
             work).RunAsync(ct);
        state.Outcome = state.Plan.Mode == SchemaDiffFixtureMode.FreshCurrentBaseline
            ? SchemaDiffApplyOutcome.CoveredByCurrentBaseline : SchemaDiffApplyOutcome.Applied;
    }

    private static async Task CleanupAsync(SchemaFixtureState state, bool acquired, List<Exception> cleanup)
    {
        state.Budget.Cleanup = true;
        if (acquired)
            foreach (var statement in state.Plan.Run.Release)
            {
                try { await SchemaFixtureCommands.ExecuteAsync(state.Connection, null, statement,
             CancellationToken.None); }
                catch (Exception error) { cleanup.Add(error); }
            }
        await SchemaResourceCleanup.DisposeAsync(state.Raw, cleanup);
    }
}
