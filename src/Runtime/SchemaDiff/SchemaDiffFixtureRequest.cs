namespace Neemle.OrmLite;

public sealed record SchemaDiffFixtureRequest(
    SchemaDiffArtifact Artifact, SchemaDiffFixtureMode Mode, SchemaDiffReview Review, SchemaDiffLineage Lineage);
