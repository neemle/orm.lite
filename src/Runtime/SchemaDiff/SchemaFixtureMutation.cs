using System.Data.Common;
using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaFixtureMutation
{
    internal static async Task RunAsync(SchemaUnitExecution execution, CancellationToken ct)
    {
        var state = execution.State; var work = execution.Work;
        var actual = await execution.Session!.GuardAndInspectAsync(execution.Transaction!, ct);
        if (work.Kind == SchemaFixtureWorkKind.Inspect)
        { SchemaFixtureChecks.Initial(state, actual); return; }
        SchemaFixtureChecks.Progress(state, actual);
        var planned = await PlansAsync(execution, ct);
        foreach (var statement in planned)
            await SchemaFixtureCommands.ExecuteAsync(state.Connection, execution.Transaction, statement, ct);
        var after = await execution.Session.GuardAndInspectAsync(execution.Transaction!, ct);
        Expected(state, work, after);
        await execution.Unit!.VerifyAsync(execution.Transaction!, ct);
        if (work.Receipt is { } receipt)
            foreach (var statement in DdlCompiler.Compile(receipt, state.Plan.Context.Provider))
                await SchemaFixtureCommands.ExecuteAsync(state.Connection, execution.Transaction, statement, ct);
        var recorded = await execution.Session.GuardAndInspectAsync(execution.Transaction!, ct);
        History(actual, recorded, work);
        execution.PostState = recorded;
    }

    private static async Task<IReadOnlyList<Statement>> PlansAsync(SchemaUnitExecution execution, CancellationToken ct)
    {
        var statements = new List<Statement>(); var state = execution.State;
        foreach (var node in execution.Work.Operations)
        {
            var planned = node is CreateTable create
                ? await execution.Session!.PlanStrictCreateAsync(create, execution.Transaction!, ct)
                : await execution.Unit!.PlanAsync(node, execution.Transaction!, ct);
            var expected = SchemaFixtureStatements.Expected(node, state.Plan.Context.Provider);
            if (!SchemaFixtureStatements.Same(expected, planned))
                throw new SchemaDiffException(SchemaDiffError.UnsupportedDifference);
            if ((long)statements.Count + planned.Count > state.Plan.Target.Limits.MaxStatements)
                throw new SchemaDiffException(SchemaDiffError.LimitExceeded);
            statements.AddRange(planned);
        }
        return statements.AsReadOnly();
    }

    private static void Expected(SchemaFixtureState state, SchemaFixtureWork work, SchemaDiffObservation after)
    {
        state.Bind(after);
        if (work.Kind == SchemaFixtureWorkKind.Revert)
        {
            if (!state.Physical(after, state.Plan.Artifact.Recipe.Before))
                throw new SchemaDiffException(SchemaDiffError.Drift);
        }
        else if (work.Kind == SchemaFixtureWorkKind.Bootstrap)
        {
            if (after.Digest != state.Progress!.Digest) throw new SchemaDiffException(SchemaDiffError.Drift);
        }
        else SchemaFixtureChecks.After(state, after);
    }

    private static void History(SchemaDiffObservation before, SchemaDiffObservation after, SchemaFixtureWork work)
    {
        if (!after.History.HistoryTableExists.GetKnown()) throw new SchemaDiffException(SchemaDiffError.
            HistoryMismatch);
        var versions = before.History.AppliedVersions.GetKnown().ToList();
        if (work.Kind == SchemaFixtureWorkKind.Revert) versions.Remove(work.Version);
        else if (work.Receipt is not null) versions.Add(work.Version);
        if (!versions.Order(StringComparer.Ordinal).SequenceEqual(after.History.AppliedVersions.GetKnown()))
            throw new SchemaDiffException(SchemaDiffError.HistoryMismatch);
    }
}
