using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace FoundationProof;

internal static class ObservationProof
{
    internal static void Run()
    {
        var e = EntityDescriptor.Of("Proof", "proof", true).Property("Id", "id", ColumnType.Int32, false, true);
        var names = new[] { "proof" };
        var target = new SchemaDiffTarget(OrmSchema.Of("proof", e), "v1",
            new SchemaDiffScope("isolated-proof", "main", names), new SchemaDiffLimits(1, 2, 5, 2, 5));
        var request = new SchemaDiffInspection(target, "proof-profile");
        var versions = new List<string>();
        var history = new SchemaObservationHistory(SchemaFact<bool>.Known(false),
            SchemaFact<IReadOnlyList<string>>.Known(versions));
        var observation = SchemaDiffObservation.FromFacts(new("identity", "proof-profile", true),
            [Missing("proof")], history, request);
        var physical = observation.Digest;
        names[0] = "changed"; versions.Add("mutated"); e.Property("New", "new", ColumnType.Text, true, false);
        Check.That(observation.Tables.Single().Name == "proof", "scope snapshot");
        Check.That(observation.History.AppliedVersions.GetKnown().Count == 0, "history snapshot");
        Check.That(observation.Digest == physical, "digest stability");
        Check.Refuses(() => SchemaFact<bool>.Unknown("not observed").GetKnown(), SchemaDiffError.UnknownMetadata);
        Check.Refuses(() => SchemaFact<bool>.Unsupported("unsupported").GetKnown(), SchemaDiffError.UnknownMetadata);
        Check.Refuses(() => SchemaDiffObservation.FromFacts(new("i", "different", true), [], history, request),
            SchemaDiffError.ScopeMismatch);
        var invalid = target with { Scope = new SchemaDiffScope("isolated-proof", "main", ["not-owned"]) };
        Check.Refuses(() => SchemaDiffObservation.FromFacts(new("i", "proof-profile", true), [], history,
            new SchemaDiffInspection(invalid, "proof-profile")), SchemaDiffError.Ownership);
        Check.Refuses(() => SchemaDiffSnapshot.Capture(target with
            { Limits = new SchemaDiffLimits(1, 1, 1, 1, 1) }), SchemaDiffError.LimitExceeded);
    }

    private static SchemaObservedTable Missing(string name)
    {
        var empty = SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known([]);
        return new SchemaObservedTable(name, SchemaFact<bool>.Known(false), [],
            new SchemaObservedTableFacts(empty, empty, empty, empty));
    }
}
