using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace FoundationProof;

internal static class TargetProof
{
    internal static SchemaDiffTarget Next(SchemaDiffTarget target)
    {
        var original = target.Schema.Entities.Single();
        var entity = EntityDescriptor.Of(original.ClrType, original.Table, true);
        foreach (var column in original.Columns)
            entity.Property(column.Property, column.Column, column.Type, column.Nullable, column.Key,
                column.Precision, column.Scale);
        entity.Property("Added", "added", ColumnType.Text, true, false);
        return target with { Schema = OrmSchema.Of(target.Schema.Token, entity), ModelVersion = "model-v2" };
    }

    internal static void AddPlan(SchemaDiffTarget target, SchemaDiffObservation observation)
    {
        var report = SchemaDiff.Compare(target, observation);
        if (report.Status != SchemaDiffStatus.Changes)
        {
            Console.WriteLine("PLAN_DIAGNOSTIC " + report.Status + " "
                + string.Join(";", report.Changes.Select(c => c.Kind + ":" + c.Category))
                + " " + string.Join(";", report.Diagnostics));
            Console.WriteLine("DEPENDENCIES " + string.Join(";",
                observation.Tables.Single().Facts.DependentObjects.GetKnown().Select(d => d.Kind + ":" + d.Name)));
        }
        Check.That(report.Status == SchemaDiffStatus.Changes, "supported nullable-column plan");
        Check.That(report.Comparison.Up.Single() is AddColumn
            { IfNotExists: false, Column: { Nullable: true, PrimaryKey: false, Default: null } }, "strict typed add");
    }
}
