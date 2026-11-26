using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaAfterProjection
{
    internal static SchemaDiffObservation Expected(SchemaDiffObservation before, IReadOnlyList<Node> up)
    {
        var tables = before.Tables.Select(table => Table(before, table, up)).ToArray();
        return SchemaDiffObservation.FromFacts(before.Identity, tables, before.History,
            new(SchemaSnapshotCopies.Target(before.Target), before.ProviderProfile));
    }

    private static SchemaObservedTable Table(SchemaDiffObservation before, SchemaObservedTable table,
        IReadOnlyList<Node> up)
    {
        var desired = before.Target.Desired.Single(t => t.Name == table.Name);
        if (up.OfType<CreateTable>().Any(n => n.Table == table.Name))
            return new(table.Name, SchemaFact<bool>.Known(true),
                desired.Columns.Select(c => Column(c, before.ProviderProfile)).ToArray(),
                SchemaCreatedInventory.Facts(desired, before.ProviderProfile));
        var columns = table.Columns.ToList();
        var added = up.OfType<AddColumn>().Where(n => n.Table == table.Name).ToArray();
        foreach (var add in added)
            columns.Add(Column(desired.Columns.Single(c => c.Column == add.Column.Column), before.ProviderProfile));
        return table with { Columns = columns.AsReadOnly(),
            Facts = AddedEffects(table.Facts, added, before.ProviderProfile) };
    }

    private static SchemaObservedTableFacts AddedEffects(SchemaObservedTableFacts facts,
        IReadOnlyList<AddColumn> added, string profile)
    {
        if (profile != "postgres:schema-diff-v1"
            || !added.Any(c => c.Column.Type is TypeRef { Kind: ColumnType.Text })) return facts;
        var dependencies = facts.DependentObjects.GetKnown();
        if (dependencies.Any(d => d.Kind == "dependent-local:toast")) return facts;
        return facts with { DependentObjects = SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known(
            dependencies.Append(SchemaLocalRoles.Toast()).ToArray()) };
    }

    private static SchemaObservedColumn Column(ColumnDescriptor column, string profile) =>
        new(column.Column, SchemaFact<SchemaStorageType>.Known(SchemaPhysicalTypes.Target(column, profile)
            ?? throw new SchemaDiffException(SchemaDiffError.InvalidArtifact)),
            SchemaFact<bool>.Known(column.Nullable && !column.Key), SchemaFact<int>.Known(column.Key ? 1 : 0),
            new(SchemaFact<SchemaDefault>.Known(new(SchemaPhysicalTypes.Default(column))),
                SchemaFact<bool>.Known(false), SchemaFact<bool>.Known(false), SchemaFact<string?>.Known(null)));
}
