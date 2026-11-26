using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal sealed class SchemaFixturePlan
{
    private SchemaFixturePlan(OrmContext context, SchemaDiffFixtureRequest request, SchemaDiffArtifact artifact,
        IReadOnlyList<string> manual)
    {
        Context = context; Mode = request.Mode; Artifact = artifact; Manual = manual;
        Target = SchemaSnapshotCopies.Target(artifact.Recipe.Before.Target);
        Current = SchemaSnapshotCopies.Target(artifact.Recipe.Before.Target).Schema;
        Planner = context.Provider.Planner ?? new StatementPlanner(context.Provider);
        Contributions = Array.AsReadOnly(Current.Contributions());
        Run = Planner.PlanRun(context.Provider);
    }

    internal OrmContext Context { get; }
    internal SchemaDiffFixtureMode Mode { get; }
    internal SchemaDiffArtifact Artifact { get; }
    internal SchemaDiffTarget Target { get; }
    internal OrmSchema Current { get; }
    internal IReadOnlyList<string> Manual { get; }
    internal IMigrationPlanner Planner { get; }
    internal IReadOnlyList<MigrationContribution> Contributions { get; }
    internal MigrationRunPlan Run { get; }
    internal SchemaDiffInspection Inspection => new(Target, Artifact.Recipe.Before.ProviderProfile);

    internal static SchemaFixturePlan Create(OrmContext context, SchemaDiffFixtureRequest request)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(request);
        if (context.DataSource is null || context.Provider is not ISchemaDiffProvider || !Enum.IsDefined(request.Mode))
            throw new SchemaDiffException(SchemaDiffError.InvalidRequest);
        var artifact = SchemaDiffArtifact.FromRecipe(request.Artifact.Recipe);
        Review(request, artifact);
        var before = artifact.Recipe.Before;
        var current = SchemaDiffSnapshot.Capture(new(request.Lineage.CurrentSchema, before.Target.ModelVersion,
            before.Target.Scope, before.Target.Limits));
        if (current.SchemaToken != before.Target.SchemaToken || !SchemaTargetIdentity.Same(current, before.Target))
            throw new SchemaDiffException(SchemaDiffError.InvalidArtifact);
        var manual = SchemaDiffNames.Limited(request.Lineage.RequiredAppliedManualVersions,
            before.Target.Limits.MaxCatalogObjects);
        SchemaDiffNames.Unique(manual);
        if (request.Mode == SchemaDiffFixtureMode.FreshCurrentBaseline && manual.Count > 0)
            throw new SchemaDiffException(SchemaDiffError.FreshLineageUnsupported);
        if (request.Mode == SchemaDiffFixtureMode.FreshCurrentBaseline) SchemaFreshPhysical.Validate(
            artifact.Recipe.After);
        var plan = new SchemaFixturePlan(context, request, artifact, manual);
        SchemaFixtureStatements.Preflight(plan);
        return plan;
    }

    private static void Review(SchemaDiffFixtureRequest request, SchemaDiffArtifact artifact)
    {
        if (request.Review.ExpectedReviewDigest != artifact.ReviewDigest)
            throw new SchemaDiffException(SchemaDiffError.ReviewMismatch);
        if (request.Review.FixtureScopeId != artifact.Recipe.Manifest.Scope.DatabaseScopeId)
            throw new SchemaDiffException(SchemaDiffError.ScopeMismatch);
    }
}
