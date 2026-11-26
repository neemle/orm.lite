namespace Neemle.OrmLite;

internal static class SchemaFactCopies
{
    internal static SchemaObservedTable Table(SchemaObservedTable table, SchemaDiffBudget budget)
    {
        ArgumentNullException.ThrowIfNull(table);
        var columns = new List<SchemaObservedColumn>();
        foreach (var column in table.Columns)
        {
            budget.Column();
            columns.Add(Column(column));
        }
        SchemaDiffNames.Unique(columns.Select(c => c.Name));
        var facts = table.Facts;
        var copied = new SchemaObservedTableFacts(Inventory(facts.Indexes, budget), Inventory(facts.Constraints, budget),
            Inventory(facts.Triggers, budget), Inventory(facts.DependentObjects, budget));
        if (table.Exists.Knowledge == SchemaKnowledge.Known && !table.Exists.GetKnown()
            && columns.Count > 0)
            throw new SchemaDiffException(SchemaDiffError.InvalidRequest);
        return new SchemaObservedTable(SchemaDiffNames.Required(table.Name), table.Exists.Copy(v => v),
            Array.AsReadOnly(columns.OrderBy(c => c.Name, StringComparer.Ordinal).ToArray()), copied);
    }

    private static SchemaObservedColumn Column(SchemaObservedColumn column)
    {
        ArgumentNullException.ThrowIfNull(column);
        var f = column.Facts;
        var facts = new SchemaObservedColumnFacts(f.Default.Copy(v => v with { }), f.Generated.Copy(v => v),
            f.Identity.Copy(v => v), f.Collation.Copy(v => v));
        return new SchemaObservedColumn(SchemaDiffNames.Required(column.Name), column.Type.Copy(v => v with { }),
            column.Nullable.Copy(v => v), column.KeyPosition.Copy(v => v), facts);
    }

    private static SchemaFact<IReadOnlyList<SchemaObjectStamp>> Inventory(
        SchemaFact<IReadOnlyList<SchemaObjectStamp>> fact, SchemaDiffBudget budget) => fact.Copy(values =>
        {
            var copied = new List<SchemaObjectStamp>();
            foreach (var value in values)
            {
                budget.Object();
                copied.Add(new SchemaObjectStamp(SchemaDiffNames.Required(value.Kind),
                    SchemaDiffNames.Required(value.Name), SchemaDiffNames.Required(value.DefinitionDigest)));
            }
            return Array.AsReadOnly(copied.OrderBy(v => v.Kind, StringComparer.Ordinal)
                .ThenBy(v => v.Name, StringComparer.Ordinal)
                .ThenBy(v => v.DefinitionDigest, StringComparer.Ordinal).ToArray());
        });

    internal static SchemaObservationHistory History(SchemaObservationHistory history, int limit)
    {
        var versions = history.AppliedVersions.Copy(v =>
        {
            var copied = SchemaDiffNames.Limited(v, limit);
            SchemaDiffNames.Unique(copied);
            return Array.AsReadOnly(copied.Order(StringComparer.Ordinal).ToArray());
        });
        if (history.HistoryTableExists.Knowledge == SchemaKnowledge.Known && !history.HistoryTableExists.GetKnown()
            && versions.Knowledge == SchemaKnowledge.Known && versions.GetKnown().Count > 0)
            throw new SchemaDiffException(SchemaDiffError.InvalidRequest);
        return new SchemaObservationHistory(history.HistoryTableExists.Copy(v => v), versions);
    }
}
