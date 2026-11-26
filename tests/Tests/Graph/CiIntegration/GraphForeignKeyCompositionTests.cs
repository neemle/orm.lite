using Neemle.OrmLite.Nodes;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.Graph.CiIntegration;

[Collection("Graph")]
public sealed class GraphForeignKeyCompositionTests
{
    [Fact(DisplayName =
        "[UC-002/003] generated FK normalization composes with typed graph identity and physical SetNull")]
    public async Task Fk_metadata_and_graph_registration_share_the_generated_model()
    {
        var (diagnostics, _, output) = Run(ComposedGraphSource.Text);
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        var key = Assert.Single(model.Descriptor("Child").ForeignKeys);
        Assert.Equal(new ForeignKeyDescriptor("fk_compose_parent", "parent_id",
            new("compose_fk_parents", "id"), ReferentialAction.SetNull), key);
        Assert.Empty(model.Descriptor("Parent").ForeignKeys);
        Assert.Equal(new[] { "compose_fk_parents", "compose_fk_children" },
            model.Schema.Baseline().OfType<CreateTable>().Select(table => table.Table));
        model.RunModuleInitializer();
        await using var db = await GraphDatabase.OpenAsync();
        await db.Context.MigrateAsync(model.Schema.Contributions());
        var operations = model.Class("ParentOrm").Assembly.GetType("Fixture.ComposedOps", true)!;
        var read = Assert.IsAssignableFrom<Task<bool>>(operations.GetMethod("Run")!.Invoke(null, [db.Context]));
        Assert.True(await read);
    }

    [Fact(DisplayName =
        "[SEC][UC-002/003] invalid FK intent still withholds the complete baseline beside graph emission")]
    public void Invalid_fk_does_not_publish_a_partial_generated_schema()
    {
        var source = ComposedGraphSource.Text.Replace("public Guid? ParentId", "public Guid ParentId");
        var (diagnostics, generated, output) = Run(source);
        Assert.Contains(diagnostics, issue => issue.Id == "ORMLITE035"
            && issue.GetMessage().Contains("NonNullableSetNull", StringComparison.Ordinal));
        Assert.DoesNotContain("class " + output.AssemblyName + "Schema", generated);
        Assert.Contains("GraphEntityBinding", generated);
    }

    [Fact(DisplayName = "[UC-002/003] graph-only declarations acquire no database FK intent")]
    public void Removing_opt_in_preserves_graph_paths_without_constraints()
    {
        var (diagnostics, _, output) = Run(ComposedGraphSource.Text.Replace(ComposedGraphSource.Intent, ""));
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        Assert.Empty(model.Descriptor("Child").ForeignKeys);
        Assert.All(model.Schema.Baseline().OfType<CreateTable>(),
            table => Assert.Empty(table.Constraints ?? []));
        model.RunModuleInitializer();
        var paths = model.Class("ParentOrm").GetNestedType("Paths")!;
        Assert.NotNull(paths.GetProperty("Children")!.GetValue(null));
    }
}
