using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.EntityShapes.NamedShapeSources;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class SameNameEntityGeneratorTests
{
    [Fact(
        DisplayName = "B20 two entities of one simple name in two namespaces both generate, each under its own "
        + "class name, with no diagnostic and no CS error")]
    public void Two_namespaces_give_two_classes()
    {
        var (diagnostics, _, output) = Run(SameName);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        var model = GeneratedModel.Of(output);
        Assert.False(model.Has("NoteOrm"));
        Assert.Equal("alpha_notes", model.Descriptor("Alpha_Note").Table);
        Assert.Equal("beta_notes", model.Descriptor("Beta_Note").Table);
        Assert.Equal("global::Fixture.Alpha.Note", model.Descriptor("Alpha_Note").ClrType);
        Assert.Equal("global::Fixture.Beta.Note", model.Descriptor("Beta_Note").ClrType);
        Assert.Equal(
            ["alpha_notes", "beta_notes", "items"],
            model.Schema.Entities.Select(e => e.Table).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact(
        DisplayName = "B20 a relation and a collection resolve their target by full symbol name, not by the "
        + "simple name")]
    public void A_relation_resolves_its_target_by_full_name()
    {
        var (diagnostics, generated, output) = Run(SameName);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        Assert.Contains("Alpha_NoteOrm.GetAsync", LoaderOf(generated, "LoadParentAsync"), StringComparison.Ordinal);
        Assert.DoesNotContain("Beta_NoteOrm", LoaderOf(generated, "LoadParentAsync"), StringComparison.Ordinal);
        Assert.Contains("Beta_NoteOrm.GetAsync", LoaderOf(generated, "LoadMateAsync"), StringComparison.Ordinal);
        Assert.DoesNotContain("Alpha_NoteOrm", LoaderOf(generated, "LoadMateAsync"), StringComparison.Ordinal);
        var model = GeneratedModel.Of(output);
        Assert.Contains("LoadItemsAsync", Methods(model, "Alpha_NoteOrm"));
        Assert.DoesNotContain("LoadItemsAsync", Methods(model, "Beta_NoteOrm"));
        Assert.Contains("ItemOrm.Query", LoaderOf(generated, "LoadItemsAsync"), StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "B20 a collection child and a many-to-many other side of a shared name are resolved by full "
        + "symbol name")]
    public void A_collection_child_and_a_linked_side_are_resolved_by_full_name()
    {
        var (diagnostics, generated, output) = Run(SameNameLinks);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        Assert.Contains("Beta_NoteOrm.Query", LoaderOf(generated, "LoadBetasAsync"), StringComparison.Ordinal);
        Assert.Contains("Alpha_NoteOrm.Map", LoaderOf(generated, "LoadAlphasAsync"), StringComparison.Ordinal);
        var methods = Methods(GeneratedModel.Of(output), "HolderOrm");
        Assert.Contains("AddAlphaAsync", methods);
        Assert.Contains("RemoveAlphaAsync", methods);
    }

    [Theory(
        DisplayName = "B20 an entity that was refused or dropped does not rename a valid entity of the same simple "
        + "name")]
    [InlineData(GenericSharesName, NameId)]
    [InlineData(StaticSharesName, TypeId)]
    public void A_refused_entity_leaves_a_valid_one_of_its_name_alone(string source, string refusalId)
    {
        var (diagnostics, generated, output) = Run(source);

        Assert.Equal(refusalId, Assert.Single(diagnostics).Id);
        Assert.Empty(CompileErrors(output));
        var model = GeneratedModel.Of(output);
        Assert.True(model.Has("NoteOrm"));
        Assert.False(model.Has("B_NoteOrm"));
        Assert.False(model.Has("A_NoteOrm"));
        Assert.Contains("LoadMateAsync", Methods(model, "HolderOrm"));
        Assert.Contains("NoteOrm.GetAsync", LoaderOf(generated, "LoadMateAsync"), StringComparison.Ordinal);
        Assert.Equal("b_notes", model.Descriptor("Note").Table);
    }

    [Fact(DisplayName = "B20 two soft-delete entities of one simple name get two history tokens and two purge classes")]
    public void Two_soft_delete_entities_get_distinct_tokens()
    {
        var (diagnostics, _, output) = Run(SameNameSoft);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        var model = GeneratedModel.Of(output);
        Assert.Equal("alpha_note", model.Soft("Alpha_Note")!.EntityToken);
        Assert.Equal("beta_note", model.Soft("Beta_Note")!.EntityToken);
        Assert.True(model.Has("Alpha_NotePurge"));
        Assert.True(model.Has("Beta_NotePurge"));
        Assert.False(model.Has("NotePurge"));
    }

    [Fact(
        DisplayName = "B20 a nested entity and a top-level entity of one simple name are told apart by the "
        + "containing type")]
    public void A_nested_and_a_top_level_entity_are_told_apart()
    {
        var (diagnostics, _, output) = Run(SameNameNested);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        var model = GeneratedModel.Of(output);
        Assert.Equal("global::Fixture.Note", model.Descriptor("Note").ClrType);
        Assert.Equal("global::Fixture.Outer.Note", model.Descriptor("Outer_Note").ClrType);
        Assert.Equal("notes", model.Descriptor("Note").Table);
        Assert.Equal("outer_notes", model.Descriptor("Outer_Note").Table);
    }
}
