namespace Neemle.OrmLite.Sqlite;

internal static class SqliteHistoryLayout
{
    internal static bool Valid(IReadOnlyList<SchemaObservedColumn> columns) => columns.Count == 2
        && Column(columns.SingleOrDefault(c => c.Name == "version"), "text", 1, SchemaDefaultKind.None)
        && Column(columns.SingleOrDefault(c => c.Name == "applied_at"), "text", 0,
            SchemaDefaultKind.CurrentTime);

    private static bool Column(SchemaObservedColumn? column, string storage, int key, SchemaDefaultKind value)
    {
        if (column is null) return false;
        var facts = column.Facts;
        return Core(column, storage, key) && Known(facts.Default, new SchemaDefault(value))
            && Known(facts.Generated, false) && Known(facts.Identity, false) && Known(facts.Collation, null);
    }

    private static bool Core(SchemaObservedColumn column, string storage, int key) =>
        Known(column.Type, new SchemaStorageType(storage, null, null)) && Known(column.Nullable, false)
        && Known(column.KeyPosition, key);

    private static bool Known<T>(SchemaFact<T> fact, T value) => fact.Knowledge == SchemaKnowledge.Known
        && EqualityComparer<T>.Default.Equals(fact.GetKnown(), value);
}
