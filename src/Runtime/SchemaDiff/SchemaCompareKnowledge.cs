namespace Neemle.OrmLite;

internal static class SchemaCompareKnowledge
{
    internal static bool Table(SchemaCompareState state, SchemaObservedTable table)
    {
        var item = new SchemaObjectRef(table.Name, null);
        var known = state.Known(table.Exists, item, "existence");
        known &= state.Known(table.Facts.Indexes, item, "indexes");
        known &= state.Known(table.Facts.Constraints, item, "constraints");
        known &= state.Known(table.Facts.Triggers, item, "triggers");
        known &= state.Known(table.Facts.DependentObjects, item, "dependencies");
        foreach (var column in table.Columns) known &= Column(state, table.Name, column);
        return known;
    }

    private static bool Column(SchemaCompareState state, string table, SchemaObservedColumn column)
    {
        var item = new SchemaObjectRef(table, column.Name);
        var known = state.Known(column.Type, item, "type");
        known &= state.Known(column.Nullable, item, "nullability");
        known &= state.Known(column.KeyPosition, item, "key");
        known &= state.Known(column.Facts.Default, item, "default");
        known &= state.Known(column.Facts.Generated, item, "generated");
        known &= state.Known(column.Facts.Identity, item, "identity");
        known &= state.Known(column.Facts.Collation, item, "collation");
        return known;
    }
}
