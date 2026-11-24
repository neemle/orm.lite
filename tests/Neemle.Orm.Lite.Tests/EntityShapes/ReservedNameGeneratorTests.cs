using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.EntityShapes.NamedShapeSources;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class ReservedNameGeneratorTests
{
    [Fact(
        DisplayName = "B20 a global-namespace static entity named like a live entity gets a reserved token, the "
        + "live entity keeps its name and its relation loader")]
    public void A_static_namesake_in_the_global_namespace_takes_the_reserved_token()
    {
        var (diagnostics, generated, output) = RunWhole(GlobalStaticSharesName);

        Assert.Equal(TypeId, Assert.Single(diagnostics).Id);
        Assert.Empty(CompileErrors(output));
        var model = GeneratedModel.Of(output);
        Assert.True(model.Has("NoteOrm"));
        Assert.Equal("b_notes", model.Descriptor("Note").Table);
        Assert.Contains("LoadMateAsync", Methods(model, "HolderOrm"));
        Assert.Contains("NoteOrm.GetAsync", LoaderOf(generated, "LoadMateAsync"), StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "B20 two refused generic entities of one simple name leave a valid namesake and its relation "
        + "loader alone")]
    public void Two_refused_generic_namesakes_leave_the_valid_one_alone()
    {
        var (diagnostics, generated, output) = RunWhole(GlobalGenericsShareName);

        Assert.Equal([NameId, NameId], diagnostics.Select(d => d.Id).ToArray());
        Assert.Empty(CompileErrors(output));
        var model = GeneratedModel.Of(output);
        Assert.True(model.Has("NoteOrm"));
        Assert.Equal("b_notes", model.Descriptor("Note").Table);
        var tables = model.Schema.Entities.Select(e => e.Table).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["b_notes", "holders"], tables);
        Assert.Contains("NoteOrm.GetAsync", LoaderOf(generated, "LoadMateAsync"), StringComparison.Ordinal);
    }
}
