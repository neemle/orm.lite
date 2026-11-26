namespace Neemle.OrmLite;

public sealed record SchemaDiffLineage(OrmSchema CurrentSchema, IReadOnlyList<string> RequiredAppliedManualVersions);
