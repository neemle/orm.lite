namespace Neemle.OrmLite;

public sealed record SchemaObservedColumn(
    string Name, SchemaFact<SchemaStorageType> Type, SchemaFact<bool> Nullable,
    SchemaFact<int> KeyPosition, SchemaObservedColumnFacts Facts);
