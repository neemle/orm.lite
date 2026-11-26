using Neemle.OrmLite;
using Neemle.OrmLite.Nodes;

namespace FaultProof;

internal sealed class FaultControl(FaultKind kind)
{
    internal const string Canary = "PRIVATE_FAKE_KEY_ROW_CREDENTIAL_117";
    internal FaultKind Kind => kind;
    internal List<string> Events { get; } = [];
    internal CancellationTokenSource Cancellation { get; } = new();
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal bool Created, Applied;
    internal SchemaDiffTarget Target { get; } = TargetOf();
    internal SchemaDiffArtifact Artifact { get; private set; } = null!;

    internal void Prepare()
    {
        var empty = SchemaFact<IReadOnlyList<SchemaObjectStamp>>.Known([]);
        var table = new SchemaObservedTable("fault-owned", SchemaFact<bool>.Known(false), [],
            new(empty, empty, empty, empty));
        var before = SchemaDiffObservation.FromFacts(new("fake-owned-source", "postgres:schema-diff-v1", true),
            [table], new(SchemaFact<bool>.Known(true),
                SchemaFact<IReadOnlyList<string>>.Known([OrmSchema.BaselinePrefix + "fault"])),
            new(Target, "postgres:schema-diff-v1"));
        Artifact = SchemaDiff.Emit(SchemaDiff.Compare(Target, before), new("0400_fault", "Fault117", "Factory"));
    }

    internal SchemaDiffObservation Observe(FaultTransaction transaction)
    {
        var recipe = Artifact.Recipe;
        var versions = new List<string> { OrmSchema.BaselinePrefix + "fault" };
        if (transaction.Applied) versions.Add(Artifact.Version);
        return SchemaDiffObservation.FromFacts(recipe.Before.Identity,
            transaction.Created ? recipe.After.Tables : recipe.Before.Tables,
            new(SchemaFact<bool>.Known(true), SchemaFact<IReadOnlyList<string>>.Known(versions)),
            new(Target, "postgres:schema-diff-v1"));
    }

    private static SchemaDiffTarget TargetOf()
    {
        var entity = EntityDescriptor.Of("Fault", "fault-owned", true)
            .Property("Id", "id", ColumnType.Text, false, true);
        return new(OrmSchema.Of("fault", entity), "fault-v1", new("fake-isolated117", "public", ["fault-owned"]),
            new(2, 10, 100, 10, 200));
    }
}
