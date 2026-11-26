using System.Data.Common;

namespace Neemle.OrmLite;

internal sealed class SchemaFixtureState(SchemaFixturePlan plan)
{
    internal SchemaFixturePlan Plan => plan;
    internal List<SchemaDiffUnitResult> Units { get; } = [];
    internal List<string> Committed { get; } = [];
    internal SchemaDiffApplyOutcome Outcome { get; set; }
    internal SchemaRestoration Restoration { get; set; }
    internal bool Bootstrap { get; set; }
    internal DbConnection Raw { get; set; } = null!;
    internal DbConnection Connection { get; set; } = null!;
    internal SchemaStatementBudget Budget { get; } = new(plan.Target.Limits.MaxStatements);
    internal SchemaObservationIdentity? Identity { get; set; }
    internal SchemaDiffObservation? Progress { get; set; }
    internal SchemaDiffObservation? Initial { get; set; }
    internal bool Repeat { get; set; }

    internal SchemaDiffApplyResult Result() => new(Outcome, Units, Committed, Bootstrap, Restoration);

    internal void Bind(SchemaDiffObservation actual)
    {
        Validate(actual);
        if (Identity is null)
        {
            if (plan.Mode != SchemaDiffFixtureMode.FreshCurrentBaseline
                && actual.Identity != plan.Artifact.Recipe.Before.Identity)
                throw new SchemaDiffException(SchemaDiffError.ScopeMismatch);
            Identity = actual.Identity;
        }
        else if (actual.Identity != Identity) throw new SchemaDiffException(SchemaDiffError.ScopeMismatch);

    }

    private void Validate(SchemaDiffObservation actual)
    {
        if (!SchemaRecipeValidation.Scope(actual.Target.Scope, plan.Artifact.Recipe.Manifest.Scope)
            || !SchemaTargetIdentity.Same(actual.Target, plan.Artifact.Recipe.Before.Target))
            throw new SchemaDiffException(SchemaDiffError.ScopeMismatch);
        if (actual.ProviderProfile != plan.Artifact.Recipe.Before.ProviderProfile || !actual.Identity.Consistent)
            throw new SchemaDiffException(SchemaDiffError.ScopeMismatch);
        if (actual.History.HistoryTableExists.Knowledge != SchemaKnowledge.Known
            || actual.History.AppliedVersions.Knowledge != SchemaKnowledge.Known)
            throw new SchemaDiffException(SchemaDiffError.UnknownMetadata);
    }

    internal bool Physical(SchemaDiffObservation actual, SchemaDiffObservation expected) =>
        SchemaObservationDigest.Physical(expected.Identity, expected.Target, actual.Tables) == expected.Digest;
}
