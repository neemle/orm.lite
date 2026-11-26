using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaCompareTables
{
    internal static void Compare(SchemaCompareState state, SchemaDesiredTable desired, SchemaObservedTable actual)
    {
        if (!SchemaCompareKnowledge.Table(state, actual)) return;
        var item = new SchemaObjectRef(desired.Name, null);
        if (desired.IsView || desired.ComplexEffects || desired.Columns.Count(c => c.Key) > 1)
        {
            state.Change(SchemaDiffChangeKind.KeyOrConstraintChange, item, SchemaDiffImpact.CompatibilityRisk,
                "unsupported-declared-effects");
            return;
        }
        if (!actual.Exists.GetKnown()) { Missing(state, desired, actual); return; }
        Existing(state, desired, actual);
    }

    private static void Missing(SchemaCompareState state, SchemaDesiredTable desired, SchemaObservedTable actual)
    {
        var item = new SchemaObjectRef(desired.Name, null);
        var operation = state.Target.Baseline.OfType<CreateTable>().SingleOrDefault(t => t.Table == desired.Name);
        if (operation is null || desired.Columns.Count == 0 || operation.Constraints is { Count: > 0 }
            || desired.Columns.Any(c => SchemaPhysicalTypes.Target(c, state.Observation.ProviderProfile) is null)
            || actual.Facts.DependentObjects.GetKnown().Count > 0)
        {
            state.Change(SchemaDiffChangeKind.KeyOrConstraintChange, item, SchemaDiffImpact.CompatibilityRisk,
                "unsupported-create-effects");
            return;
        }
        state.Change(SchemaDiffChangeKind.CreateTable, item, SchemaDiffImpact.Additive, "plain-managed-table");
        state.Operation(operation);
    }

    private static void Existing(SchemaCompareState state, SchemaDesiredTable desired, SchemaObservedTable actual)
    {
        foreach (var column in desired.Columns)
        {
            var found = actual.Columns.SingleOrDefault(c => c.Name == column.Column);
            if (found is null) SchemaCompareAdd.Column(state, desired, actual, column);
            else SchemaCompareColumns.Existing(state, desired.Name, column, found);
        }
        foreach (var extra in actual.Columns.Where(c => desired.Columns.All(d => d.Column != c.Name)))
            state.Change(SchemaDiffChangeKind.DropColumn, new(desired.Name, extra.Name),
                SchemaDiffImpact.Destructive, "absent-from-target");
    }
}
