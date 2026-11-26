namespace Neemle.OrmLite;

public sealed record SchemaObservedTable(
    string Name, SchemaFact<bool> Exists, IReadOnlyList<SchemaObservedColumn> Columns, SchemaObservedTableFacts Facts);
