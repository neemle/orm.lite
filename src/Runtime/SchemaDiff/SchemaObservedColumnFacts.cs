namespace Neemle.OrmLite;

public sealed record SchemaObservedColumnFacts(
    SchemaFact<SchemaDefault> Default, SchemaFact<bool> Generated,
    SchemaFact<bool> Identity, SchemaFact<string?> Collation);
