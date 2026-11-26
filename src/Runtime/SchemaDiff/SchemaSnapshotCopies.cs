namespace Neemle.OrmLite;

internal static class SchemaSnapshotCopies
{
    internal static SchemaDiffTarget Target(SchemaDiffSnapshot snapshot)
    {
        var entities = snapshot.Desired.Select(Entity).ToArray();
        return new SchemaDiffTarget(OrmSchema.Of(snapshot.SchemaToken, entities), snapshot.ModelVersion,
            snapshot.Scope with { OwnedTables = Array.AsReadOnly(snapshot.Scope.OwnedTables.ToArray()) },
            snapshot.Limits with { });
    }

    private static EntityDescriptor Entity(SchemaDesiredTable table)
    {
        if (table.IsView || table.ComplexEffects) throw new SchemaDiffException(SchemaDiffError.InvalidArtifact);
        var entity = EntityDescriptor.Of(table.Name, table.Name, true);
        foreach (var column in table.Columns)
            entity.Property(column.Property, column.Column, column.Type, column.Nullable, column.Key,
                column.Precision, column.Scale);
        return entity;
    }

    internal static SchemaDiffObservation Observation(SchemaDiffObservation observation)
    {
        var request = new SchemaDiffInspection(Target(observation.Target), observation.ProviderProfile);
        return SchemaDiffObservation.FromFacts(observation.Identity, observation.Tables, observation.History, request);
    }
}
