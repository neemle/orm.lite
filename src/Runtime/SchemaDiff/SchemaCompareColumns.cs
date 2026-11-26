namespace Neemle.OrmLite;

internal static class SchemaCompareColumns
{
    internal static void Existing(SchemaCompareState state, string table,
        ColumnDescriptor desired, SchemaObservedColumn actual)
    {
        var item = new SchemaObjectRef(table, desired.Column);
        var type = SchemaPhysicalTypes.Target(desired, state.Observation.ProviderProfile);
        if (type is null)
        {
            state.Change(SchemaDiffChangeKind.TypeChange, item, SchemaDiffImpact.CompatibilityRisk, "unsupported-type");
            return;
        }
        if (type != actual.Type.GetKnown())
            state.Change(SchemaDiffChangeKind.TypeChange, item, SchemaDiffImpact.CompatibilityRisk, "physical-type");
        if ((desired.Nullable && !desired.Key) != actual.Nullable.GetKnown())
            state.Change(SchemaDiffChangeKind.NullabilityChange, item, SchemaDiffImpact.CompatibilityRisk, "nullability");
        if ((desired.Key ? 1 : 0) != actual.KeyPosition.GetKnown())
            state.Change(SchemaDiffChangeKind.KeyOrConstraintChange, item, SchemaDiffImpact.CompatibilityRisk, "key");
        if (SchemaPhysicalTypes.Default(desired) != actual.Facts.Default.GetKnown().Kind)
            state.Change(SchemaDiffChangeKind.DefaultChange, item, SchemaDiffImpact.CompatibilityRisk, "default");
        if (actual.Facts.Generated.GetKnown() || actual.Facts.Identity.GetKnown()
            || actual.Facts.Collation.GetKnown() is not null)
            state.Change(SchemaDiffChangeKind.KeyOrConstraintChange, item, SchemaDiffImpact.CompatibilityRisk,
                "generated-identity-collation");
    }
}
