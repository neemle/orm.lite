using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite;

internal static class SchemaCompareAdd
{
    internal static void Column(SchemaCompareState state, SchemaDesiredTable desired,
        SchemaObservedTable actual, ColumnDescriptor column)
    {
        var item = new SchemaObjectRef(desired.Name, column.Column);
        var create = state.Target.Baseline.OfType<CreateTable>().Single(t => t.Table == desired.Name);
        var node = create.Columns.OfType<ColumnDef>().Single(c => c.Column == column.Column);
        if (!column.Nullable || column.Key || node.Default is not null
            || SchemaPhysicalTypes.Target(column, state.Observation.ProviderProfile) is null || Effects(actual))
        {
            state.Change(SchemaDiffChangeKind.KeyOrConstraintChange, item, SchemaDiffImpact.CompatibilityRisk,
                "unsupported-add-effects");
            return;
        }
        state.Change(SchemaDiffChangeKind.AddNullableColumn, item, SchemaDiffImpact.Additive, "nullable-no-default");
        state.Operation(new AddColumn(desired.Name, node, IfNotExists: false));
    }

    private static bool Effects(SchemaObservedTable table)
    {
        var constraints = table.Facts.Constraints.GetKnown();
        var primaryNames = constraints.Where(c => c.Kind == "constraint:p").Select(c => c.Name).ToHashSet();
        return table.Facts.Triggers.GetKnown().Count > 0
            || constraints.Any(c => c.Kind is not ("constraint:p" or "constraint:n"))
            || table.Facts.DependentObjects.GetKnown().Any(c =>
                !c.Kind.StartsWith("dependent-local:", StringComparison.Ordinal))
            || table.Facts.Indexes.GetKnown().Any(c => c.Kind != "index:pk" && !primaryNames.Contains(c.Name));
    }
}
