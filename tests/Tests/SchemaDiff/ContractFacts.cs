using Neemle.OrmLite.Nodes;

namespace Neemle.OrmLite.Tests.SchemaDiff;

internal static class ContractFacts
{
    internal const string Profile = "sqlite:schema-diff-v1";

    internal static EntityDescriptor Entity(string table = "spec_items") =>
        EntityDescriptor.Of("SpecItem", table, true)
            .Property("Id", "id", ColumnType.Int32, false, true)
            .Property("Note", "note", ColumnType.Text, true, false);

    internal static SchemaDiffTarget Target(params EntityDescriptor[] entities) =>
        new(OrmSchema.Of("independent117", entities), "contract-v1",
            new("owned-independent-fixture", "main", entities.Select(e => e.Table).ToArray()),
            new(20, 100, 100, 40, 200));

    internal static SchemaFact<IReadOnlyList<SchemaObjectStamp>> Empty() =>
        SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known([]);

    internal static SchemaObservedTableFacts Inventory() => new(Empty(), Empty(), Empty(), Empty());

    internal static SchemaObservedColumn Column(string name, bool nullable = true) =>
        new(name, SchemaFact<SchemaStorageType>.Known(new(name == "id" ? "integer" : "text", null, null)),
            SchemaFact<bool>.Known(nullable), SchemaFact<int>.Known(name == "id" ? 1 : 0),
            new(SchemaFact<SchemaDefault>.Known(new(SchemaDefaultKind.None)),
                SchemaFact<bool>.Known(false), SchemaFact<bool>.Known(false), SchemaFact<string?>.Known(null)));

    internal static SchemaObservedTable Table(string name = "spec_items") =>
        new(name, SchemaFact<bool>.Known(true), [Column("id", false), Column("note")], Inventory());

    internal static SchemaObservedTable Missing(string name) =>
        new(name, SchemaFact<bool>.Known(false), [], Inventory());

    internal static SchemaObservationHistory History() =>
        new(SchemaFact<bool>.Known(true), SchemaFact<IReadOnlyList<string>>.Known(
            [OrmSchema.BaselinePrefix + "independent117"]));

    internal static SchemaDiffObservation Observe(SchemaDiffTarget target, params SchemaObservedTable[] tables) =>
        SchemaDiffObservation.FromFacts(new("database-identity", Profile, true), tables, History(),
            new(target, Profile));

    internal static SchemaDiffArtifact Emit(SchemaDiffTarget target, params SchemaObservedTable[] tables) =>
        global::Neemle.OrmLite.SchemaDiff.Emit(global::Neemle.OrmLite.SchemaDiff.Compare(target,
            Observe(target, tables)), new("0042_independent", "Contract.Generated", "Recipe"));
}
