namespace Neemle.OrmLite;

public sealed record SchemaDiffUnitResult(string Version, SchemaUnitOutcome Outcome, SchemaRestoration Restoration);
