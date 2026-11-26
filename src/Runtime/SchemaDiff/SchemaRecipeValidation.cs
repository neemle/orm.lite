namespace Neemle.OrmLite;

internal static class SchemaRecipeValidation
{
    internal static SchemaDiffRecipe Copy(SchemaDiffRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(recipe.Manifest);
        var before = SchemaSnapshotCopies.Observation(recipe.Before);
        var after = SchemaSnapshotCopies.Observation(recipe.After);
        var up = SchemaNodeCopies.Up(recipe.Nodes.Up, before.Target.Limits.MaxOperations);
        var down = SchemaNodeCopies.Down(up);
        if (!down.SequenceEqual(recipe.Nodes.Down)) throw new SchemaDiffException(SchemaDiffError.InvalidArtifact);
        var scope = recipe.Manifest.Scope with
        { OwnedTables = Array.AsReadOnly(recipe.Manifest.Scope.OwnedTables.ToArray()) };
        var manifest = recipe.Manifest with { Identity = recipe.Manifest.Identity with { }, Scope = scope,
            Provenance = recipe.Manifest.Provenance with { } };
        var copied = new SchemaDiffRecipe(manifest, new(up, down), before, after);
        Validate(copied);
        return copied;
    }

    private static void Validate(SchemaDiffRecipe recipe)
    {
        var before = recipe.Before; var after = recipe.After; var manifest = recipe.Manifest;
        var target = SchemaSnapshotCopies.Target(before.Target);
        var report = SchemaDiff.Compare(target, before);
        if (report.Status != SchemaDiffStatus.Changes || !report.Comparison.Up.SequenceEqual(recipe.Nodes.Up)
            || SchemaDiff.Compare(target, after).Status != SchemaDiffStatus.NoOp
            || before.Identity != after.Identity || before.HistoryDigest != after.HistoryDigest
            || SchemaAfterProjection.Existing(before, recipe.Nodes.Up).Digest != after.Digest)
            throw new SchemaDiffException(SchemaDiffError.InvalidArtifact);
        if (manifest.SchemaToken != before.Target.SchemaToken || !Scope(manifest.Scope, before.Target.Scope)
            || manifest.Provenance != new SchemaDiffProvenance(before.Target.ModelVersion,
                SchemaRecipeIdentity.Target(before.Target), before.Digest, before.ProviderProfile))
            throw new SchemaDiffException(SchemaDiffError.InvalidArtifact);
        SchemaSourceNames.Validate(manifest.Identity);
        var versions = before.History.AppliedVersions.GetKnown();
        if (versions.Any(v => !Reserved(v) && StringComparer.Ordinal.Compare(v, manifest.Identity.VersionPrefix) >= 0))
            throw new SchemaDiffException(SchemaDiffError.HistoryMismatch);
    }

    private static bool Reserved(string value) => value.StartsWith("0000_", StringComparison.Ordinal)
        || value.StartsWith("0001_", StringComparison.Ordinal) || value.StartsWith("9900_", StringComparison.Ordinal);

    internal static bool Scope(SchemaDiffScope left, SchemaDiffScope right) =>
        left.DatabaseScopeId == right.DatabaseScopeId && left.Namespace == right.Namespace
        && left.OwnedTables.Order(StringComparer.Ordinal).SequenceEqual(right.OwnedTables.Order(StringComparer.Ordinal));
}
