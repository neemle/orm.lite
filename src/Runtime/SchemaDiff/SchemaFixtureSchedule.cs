using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaFixtureSchedule
{
    internal static IReadOnlyList<SchemaFixtureWork> Build(SchemaFixtureState state)
    {
        if (state.Repeat) return [];
        var plan = state.Plan; var artifact = plan.Artifact;
        if (plan.Mode == SchemaDiffFixtureMode.RevertAppliedUpgrade)
            return [new(SchemaFixtureWorkKind.Revert, artifact.Version, artifact.Recipe.Nodes.Down,
                SchemaFixtureStatements.Forget(artifact.Version))];
        if (plan.Mode == SchemaDiffFixtureMode.UpgradeObserved)
            return [new(SchemaFixtureWorkKind.Upgrade, artifact.Version, artifact.Recipe.Nodes.Up,
                MigrationRunner.RecordApplied(artifact.Version))];
        var work = new List<SchemaFixtureWork>();
        if (!state.Progress!.History.HistoryTableExists.GetKnown())
            work.Add(new(SchemaFixtureWorkKind.Bootstrap, MigrationRunner.HistoryTable,
                [MigrationRunner.CreateHistoryTable()], null));
        foreach (var contribution in plan.Contributions)
            work.Add(new(SchemaFixtureWorkKind.FreshContribution, contribution.Version,
                Array.AsReadOnly(contribution.Nodes!(plan.Context.Provider).ToArray()),
                MigrationRunner.RecordApplied(contribution.Version)));
        work.Add(new(SchemaFixtureWorkKind.Coverage, artifact.CoverageVersion, [],
            MigrationRunner.RecordApplied(artifact.CoverageVersion)));
        return work.AsReadOnly();
    }
}
