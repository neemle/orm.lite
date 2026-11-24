using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeSources;
using static Neemle.Orm.Lite.Tests.EntityShapes.NamedShapeSources;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class NestedEntityGeneratorTests
{
    [Theory(
        DisplayName = "B19 a nested entity is generated under its full symbol name with no diagnostic and no CS "
        + "error")]
    [InlineData(NestedPublic, "global::Fixture.Outer.Inner")]
    [InlineData(NestedDeep, "global::Fixture.Outer.Mid.Inner")]
    [InlineData(NestedInternal, "global::Fixture.Outer.Inner")]
    [InlineData(NestedInInternal, "global::Fixture.Outer.Inner")]
    [InlineData(NestedProtectedInternal, "global::Fixture.Outer.Inner")]
    public void A_nested_entity_is_generated_by_its_full_name(string source, string full)
    {
        var (diagnostics, generated, output) = Run(source + Sibling);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        var model = GeneratedModel.Of(output);
        Assert.Equal(full, model.Descriptor("Inner").ClrType);
        Assert.Contains(full, generated, StringComparison.Ordinal);
        Assert.Contains("InsertAsync", Methods(model, "InnerOrm"));
        Assert.Contains("GetAsync", Methods(model, "InnerOrm"));
        Assert.Contains("class SibOrm", generated, StringComparison.Ordinal);
    }

    [Theory(
        DisplayName = "B19 the generated class is public only when the entity and every type around it is public")]
    [InlineData(NestedPublic, true)]
    [InlineData(NestedInternal, false)]
    [InlineData(NestedInInternal, false)]
    [InlineData(NestedProtectedInternal, false)]
    public void The_generated_class_is_as_public_as_the_whole_chain(string source, bool isPublic)
    {
        var (_, _, output) = Run(source);

        Assert.Equal(isPublic, GeneratedModel.Of(output).Class("InnerOrm").IsPublic);
    }

    [Fact(DisplayName = "B19 a nested entity in the global namespace is named from the global namespace")]
    public void A_nested_entity_in_the_global_namespace_is_generated()
    {
        var (diagnostics, _, output) = RunWhole(GlobalNested);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        Assert.Equal("global::Outer.Inner", GeneratedModel.Of(output).Descriptor("Inner").ClrType);
    }

    [Fact(DisplayName = "B19 a nested soft-delete entity gets its purge class, a nested relation gets its loaders")]
    public void A_nested_entity_keeps_its_purge_class_and_its_loaders()
    {
        var (soft, _, softOutput) = Run(NestedSoft);
        var (linked, generated, linkedOutput) = Run(NestedLinked);

        Assert.Empty(soft);
        Assert.Empty(linked);
        Assert.Empty(CompileErrors(softOutput));
        Assert.Empty(CompileErrors(linkedOutput));
        Assert.True(GeneratedModel.Of(softOutput).Has("InnerPurge"));
        var model = GeneratedModel.Of(linkedOutput);
        Assert.Contains("LoadItemAsync", Methods(model, "HolderOrm"));
        Assert.Contains("LoadHoldersAsync", Methods(model, "InnerOrm"));
        Assert.Contains("InnerOrm.GetAsync", LoaderOf(generated, "LoadItemAsync"), StringComparison.Ordinal);
    }
}
