using System.Reflection;
using Neemle.OrmLite.Postgres;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.GraphSave;

[Collection("Graph")]
public sealed class SaveLoadedTypeTests
{
    private const string Source = """
        [Table("gs112_load_isolation")]
        public sealed class LoadedRoot
        {
            [Key] public Guid Id { get; set; }
            public string Name { get; set; } = "isolation";
        }
        public static class LoadedEntry
        {
            public static System.Threading.Tasks.Task<GraphSaveResult> Save(OrmContext context) =>
                GraphSave.SaveAsync(context, new GraphSaveRequest<LoadedRoot>(new(), GraphSaveIntent.Insert,
                    new(GraphSaveIntents.Empty, GraphSavePermissions.None,
                        new(new(1, 1, 1), new(1, 1, 1)))));
        }
        """;

    [Fact(DisplayName = "[UC-006][AC-112-02] equal assembly-qualified names in separate load contexts "
        + "keep isolated bindings")]
    public async Task Independently_loaded_types_keep_their_own_metadata_and_save_binding()
    {
        var (diagnostics, _, output) = Run(Source);
        Assert.Empty(diagnostics);
        var first = GeneratedModel.Of(output);
        var second = GeneratedModel.Of(output);
        await Task.WhenAll(Task.Run(first.RunModuleInitializer), Task.Run(second.RunModuleInitializer));
        var a = EntityType(first);
        var b = EntityType(second);
        Assert.NotSame(a, b);
        Assert.Equal(a.AssemblyQualifiedName, b.AssemblyQualifiedName);
        Assert.Same(first.Descriptor("LoadedRoot"), OrmMeta.ReadDescriptor(a));
        Assert.Same(second.Descriptor("LoadedRoot"), OrmMeta.ReadDescriptor(b));
        Assert.NotSame(OrmMeta.ReadDescriptor(a), OrmMeta.ReadDescriptor(b));
        await Admitted(second);
        await Admitted(first);
    }

    private static Type EntityType(GeneratedModel model) =>
        model.Class("LoadedRootOrm").Assembly.GetType("Fixture.LoadedRoot", true)!;

    private static async Task Admitted(GeneratedModel model)
    {
        var entry = model.Class("LoadedRootOrm").Assembly.GetType("Fixture.LoadedEntry", true)!;
        await using var source = new SaveProbeSource();
        await using var context = new OrmContext(source, PostgresProvider.Instance);
        var call = (Task<GraphSaveResult>)entry.GetMethod("Save", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, [context])!;
        var error = await Assert.ThrowsAsync<GraphSaveException>(() => call);
        Assert.Equal(GraphSaveError.ProviderFailure, error.Error);
        Assert.Equal(GraphSaveOutcome.NotStarted, error.Outcome);
        Assert.Same(source.Stop, error.InnerException);
        Assert.Equal(1, source.Opens);
    }
}
