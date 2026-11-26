namespace Neemle.OrmLite;

internal static class SchemaFixtureChecks
{
    internal static void Initial(SchemaFixtureState state, SchemaDiffObservation actual)
    {
        RefuseCoveredRevert(state, actual);
        state.Bind(actual); state.Initial = actual; state.Progress = actual;
        var versions = actual.History.AppliedVersions.GetKnown(); var artifact = state.Plan.Artifact;
        var executed = versions.Contains(artifact.Version, StringComparer.Ordinal);
        var covered = versions.Contains(artifact.CoverageVersion, StringComparer.Ordinal);
        if (executed && covered) throw new SchemaDiffException(SchemaDiffError.HistoryMismatch);
        if (state.Plan.Mode == SchemaDiffFixtureMode.RevertAppliedUpgrade)
        {
            if (!executed) throw new SchemaDiffException(SchemaDiffError.HistoryMismatch);
            After(state, actual); Anchors(state, versions); return;
        }
        if (executed || covered) { Repeat(state, actual, covered); return; }
        if (state.Plan.Mode == SchemaDiffFixtureMode.FreshCurrentBaseline) Fresh(state, actual);
        else Upgrade(state, actual);
    }

    private static void RefuseCoveredRevert(SchemaFixtureState state, SchemaDiffObservation actual)
    {
        if (state.Plan.Mode == SchemaDiffFixtureMode.RevertAppliedUpgrade
            && actual.History.AppliedVersions.Knowledge == SchemaKnowledge.Known
            && actual.History.AppliedVersions.GetKnown().Contains(state.Plan.Artifact.CoverageVersion,
                StringComparer.Ordinal)) throw new SchemaDiffException(SchemaDiffError.BaselineOwnsObjects);
    }

    private static void Repeat(SchemaFixtureState state, SchemaDiffObservation actual, bool covered)
    {
        var versions = actual.History.AppliedVersions.GetKnown();
        After(state, actual); Anchors(state, versions);
        if (covered && !state.Plan.Contributions.All(c => versions.Contains(c.Version, StringComparer.Ordinal)))
            throw new SchemaDiffException(SchemaDiffError.HistoryMismatch);
        state.Repeat = true;
        state.Outcome = covered ? SchemaDiffApplyOutcome.CoveredByCurrentBaseline
            : SchemaDiffApplyOutcome.AlreadyApplied;
    }

    private static void Upgrade(SchemaFixtureState state, SchemaDiffObservation actual)
    {
        var expected = state.Plan.Artifact.Recipe.Before;
        if (actual.Digest != expected.Digest) throw new SchemaDiffException(SchemaDiffError.Drift);
        var versions = actual.History.AppliedVersions.GetKnown();
        if (!actual.History.HistoryTableExists.GetKnown())
            throw new SchemaDiffException(SchemaDiffError.RequiresManualMigrationFirst);
        Anchors(state, versions);
        if (!expected.History.AppliedVersions.GetKnown().All(v => versions.Contains(v, StringComparer.Ordinal)))
            throw new SchemaDiffException(SchemaDiffError.HistoryMismatch);
    }

    private static void Fresh(SchemaFixtureState state, SchemaDiffObservation actual)
    {
        var report = SchemaDiff.Compare(state.Plan.Target, actual);
        if (report.Status == SchemaDiffStatus.Incomplete) throw new SchemaDiffException(SchemaDiffError.
            UnknownMetadata);
        if (report.Status != SchemaDiffStatus.Changes || actual.Tables.Any(t => t.Exists.GetKnown()))
            throw new SchemaDiffException(SchemaDiffError.FreshLineageUnsupported);
        var versions = actual.History.AppliedVersions.GetKnown();
        if (state.Plan.Contributions.Any(c => versions.Contains(c.Version, StringComparer.Ordinal))
            || versions.Any(v => !Automatic(v)))
            throw new SchemaDiffException(SchemaDiffError.FreshLineageUnsupported);
    }

    private static bool Automatic(string version) => version.StartsWith(OrmSchema.BaselinePrefix,
             StringComparison.Ordinal)
        || version.StartsWith(OrmSchema.IndexesPrefix, StringComparison.Ordinal)
        || version.StartsWith(OrmSchema.ReconcilePrefix, StringComparison.Ordinal)
        || version.StartsWith(RowHistoryDdl.VersionPrefix, StringComparison.Ordinal);

    private static void Anchors(SchemaFixtureState state, IReadOnlyList<string> versions)
    {
        if (!versions.Contains(OrmSchema.BaselinePrefix + state.Plan.Target.Schema.Token, StringComparer.Ordinal)
            || !state.Plan.Manual.All(v => versions.Contains(v, StringComparer.Ordinal)))
            throw new SchemaDiffException(SchemaDiffError.RequiresManualMigrationFirst);
        if (state.Plan.Mode != SchemaDiffFixtureMode.FreshCurrentBaseline
            && !state.Plan.Artifact.Recipe.Before.History.AppliedVersions.GetKnown()
                .All(v => versions.Contains(v, StringComparer.Ordinal)))
            throw new SchemaDiffException(SchemaDiffError.HistoryMismatch);
    }

    internal static void Progress(SchemaFixtureState state, SchemaDiffObservation actual)
    {
        state.Bind(actual);
        if (state.Progress is not { } expected || actual.Digest != expected.Digest)
            throw new SchemaDiffException(SchemaDiffError.Drift);
        if (actual.HistoryDigest != expected.HistoryDigest) throw new SchemaDiffException(
            SchemaDiffError.HistoryMismatch);
    }

    internal static void After(SchemaFixtureState state, SchemaDiffObservation actual)
    {
        var equal = state.Plan.Mode == SchemaDiffFixtureMode.FreshCurrentBaseline
            ? SchemaFreshPhysical.Same(state, actual) : state.Physical(actual, state.Plan.Artifact.Recipe.After);
        if (!equal) throw new SchemaDiffException(SchemaDiffError.Drift);
    }
}
