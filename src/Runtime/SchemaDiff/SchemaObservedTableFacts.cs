namespace Neemle.OrmLite;

public sealed record SchemaObservedTableFacts(
    SchemaFact<IReadOnlyList<SchemaObjectStamp>> Indexes, SchemaFact<IReadOnlyList<SchemaObjectStamp>> Constraints,
    SchemaFact<IReadOnlyList<SchemaObjectStamp>> Triggers, SchemaFact<IReadOnlyList<SchemaObjectStamp>> DependentObjects);
