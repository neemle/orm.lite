namespace Neemle.OrmLite;

public sealed record SchemaObservationHistory(
    SchemaFact<bool> HistoryTableExists, SchemaFact<IReadOnlyList<string>> AppliedVersions);
