using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaAfterProjection
{
    internal static SchemaDiffObservation Existing(SchemaDiffObservation before, IReadOnlyList<Node> up)
    {
        if (up.Any(n => n is not AddColumn))
            throw new SchemaDiffException(SchemaDiffError.UnsupportedDifference);
        var tables = before.Tables.Select(table => Table(before, table, up)).ToArray();
        return SchemaDiffObservation.FromFacts(before.Identity, tables, before.History,
            new(SchemaSnapshotCopies.Target(before.Target), before.ProviderProfile));
    }

    private static SchemaObservedTable Table(SchemaDiffObservation before, SchemaObservedTable table,
        IReadOnlyList<Node> up)
    {
        var columns = table.Columns.ToList();
        foreach (var add in up.OfType<AddColumn>().Where(n => n.Table == table.Name))
        {
            var desired = before.Target.Desired.Single(t => t.Name == table.Name).Columns
                .Single(c => c.Column == add.Column.Column);
            var type = SchemaPhysicalTypes.Target(desired, before.ProviderProfile)
                ?? throw new SchemaDiffException(SchemaDiffError.InvalidArtifact);
            columns.Add(new(add.Column.Column, SchemaFact<SchemaStorageType>.Known(type), SchemaFact<bool>.Known(true),
                SchemaFact<int>.Known(0), new(SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.None)),
                    SchemaFact<bool>.Known(false), SchemaFact<bool>.Known(false), SchemaFact<string?>.Known(null))));
        }
        return table with { Columns = columns.AsReadOnly() };
    }
}
