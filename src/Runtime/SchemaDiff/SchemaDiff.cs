namespace Neemle.OrmLite;

public static class SchemaDiff
{
    public static Task<SchemaDiffObservation> ObserveAsync(OrmContext context, SchemaDiffTarget target,
        CancellationToken ct = default) => SchemaObserve.RunAsync(context, target, ct);

    public static SchemaDiffReport Compare(SchemaDiffTarget target, SchemaDiffObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var snapshot = SchemaDiffSnapshot.Capture(target);
        ValidateTarget(snapshot, observation.Target);
        var state = new SchemaCompareState(snapshot, observation);
        if (!observation.Identity.Consistent)
        { state.Incomplete = true; state.Diagnostics.Add("inconsistent-observation"); }
        if (!SchemaPhysicalTypes.Supported(observation.ProviderProfile))
        { state.Incomplete = true; state.Diagnostics.Add("unsupported-provider-profile"); }
        var history = new SchemaObjectRef("schema_migrations", null);
        state.Known(observation.History.HistoryTableExists, history, "history-existence");
        state.Known(observation.History.AppliedVersions, history, "history-versions");
        foreach (var table in snapshot.Desired)
            SchemaCompareTables.Compare(state, table, observation.Tables.Single(t => t.Name == table.Name));
        Lineage(state);
        return state.Report();
    }

    public static SchemaDiffArtifact Emit(SchemaDiffReport report, SchemaDiffIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Status != SchemaDiffStatus.Changes)
            throw new SchemaDiffException(SchemaDiffError.UnsupportedDifference);
        var before = report.Comparison.Before;
        var manifest = new SchemaDiffManifest(identity, before.Target.Scope,
            new(before.Target.ModelVersion, SchemaRecipeIdentity.Target(before.Target), before.Digest,
                before.ProviderProfile), before.Target.SchemaToken);
        var up = SchemaNodeCopies.Up(report.Comparison.Up, before.Target.Limits.MaxOperations);
        return SchemaDiffArtifact.FromRecipe(new(manifest, new(up, SchemaNodeCopies.Down(up)), before,
            SchemaAfterProjection.Expected(before, up)));
    }

    private static void ValidateTarget(SchemaDiffSnapshot current, SchemaDiffSnapshot observed)
    {
        if (current.SchemaToken != observed.SchemaToken || current.ModelVersion != observed.ModelVersion
            || current.Scope.DatabaseScopeId != observed.Scope.DatabaseScopeId
            || current.Scope.Namespace != observed.Scope.Namespace || !SchemaTargetIdentity.Same(current, observed)
            || !current.Scope.OwnedTables.Order(StringComparer.Ordinal)
                .SequenceEqual(observed.Scope.OwnedTables.Order(StringComparer.Ordinal)))
            throw new SchemaDiffException(SchemaDiffError.ScopeMismatch);
    }

    private static void Lineage(SchemaCompareState state)
    {
        if (state.Up.Count == 0 || !state.Observation.Tables.Any(t => t.Exists.Knowledge == SchemaKnowledge.Known
            && t.Exists.GetKnown())) return;
        var history = state.Observation.History;
        if (history.HistoryTableExists.Knowledge != SchemaKnowledge.Known
            || history.AppliedVersions.Knowledge != SchemaKnowledge.Known) return;
        if (!history.HistoryTableExists.GetKnown()
            || !history.AppliedVersions.GetKnown().Contains(OrmSchema.BaselinePrefix + state.Target.SchemaToken))
        {
            state.Manual = true; state.Diagnostics.Add("requires-settled-baseline-and-manual-anchors");
        }
    }
}
