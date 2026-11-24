using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.OrmLite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.OrmLite.Tests.EntityShapes.InheritedShapeSources;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.EntityShapes;

public sealed class InheritedMemberGeneratorTests
{
    [Fact(
        DisplayName = "B21 the key and the soft-delete column of a base class are walked, base members first")]
    public void A_base_key_and_soft_column_are_mapped()
    {
        var (diagnostics, _, output) = Run(KeyAndSoft);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        var model = GeneratedModel.Of(output);
        Assert.Equal(["Id", "DeletedAt", "Title"], Columns(model, "Doc"));
        Assert.Equal("Id", Assert.Single(model.Descriptor("Doc").Columns, c => c.Key).Property);
        // SoftDeleteMeta.Column holds the PROPERTY name (EntityDescriptor.SoftDelete(property, column, token));
        // the table column is read through the descriptor.
        Assert.Equal("DeletedAt", model.Soft("Doc")!.Column);
        Assert.Equal("deleted_at", model.Descriptor("Doc").ColumnOf("DeletedAt"));
        Assert.Contains("GetAsync", Methods(model, "DocOrm"));
        Assert.Contains("DeleteAsync", Methods(model, "DocOrm"));
    }

    [Fact(DisplayName = "B21 a closed generic base class supplies its members with the type arguments applied")]
    public void A_generic_base_supplies_a_typed_key()
    {
        var (diagnostics, _, output) = Run(GenericBase);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        var model = GeneratedModel.Of(output);
        Assert.Equal(["Id", "Name", "Slices"], Columns(model, "Pie"));
        var key = Assert.Single(model.Descriptor("Pie").Columns, c => c.Key);
        Assert.Equal("Int32", key.Type.ToString());
    }

    [Fact(DisplayName = "B21 an override or a hiding member replaces the base member once, in the base position")]
    public void The_most_derived_member_wins_a_name()
    {
        var (diagnostics, _, output) = Run(Hiding);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        var model = GeneratedModel.Of(output);
        Assert.Equal(["Id", "Label", "Note", "Rank"], Columns(model, "Leaf"));
        Assert.Equal(
            ["id", "mid_label", "leaf_note", "rank"],
            model.Descriptor("Leaf").Columns.Select(c => c.Column).ToArray());
    }

    [Fact(DisplayName = "B21 an inherited required member is built in the initializer, no CS9035 in the Map")]
    public void An_inherited_required_member_is_built_in_the_initializer()
    {
        var (diagnostics, generated, output) = Run(Required);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        Assert.Contains("Author = r.GetString(o1)", MapOf(generated, "Post"), StringComparison.Ordinal);
        Assert.Equal(["Id", "Author", "Title"], Columns(GeneratedModel.Of(output), "Post"));
    }

    [Fact(
        DisplayName = "B21 an inherited required member a row cannot populate is refused once and the Map throws")]
    public void An_inherited_required_member_that_cannot_be_populated_is_refused()
    {
        var (diagnostics, _, output) = Run(RequiredScratch);

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(PropertyId, refusal.Id);
        Assert.Contains("'Post.Scratch'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("is required", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("Scratch", SpanText(refusal));
        Assert.Empty(CompileErrors(output));
        Assert.Contains("'Scratch'", MapThrows(GeneratedModel.Of(output), "Post").Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "B21 an inherited private setter is refused once on the member, as a declared one is")]
    public void An_inherited_private_setter_is_refused_once()
    {
        var (diagnostics, _, output) = Run(PrivateSetter);

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(PropertyId, refusal.Id);
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'Doc.Secret'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("private", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("Secret", SpanText(refusal));
        Assert.Empty(CompileErrors(output));
        Assert.Equal(["Id", "Title"], Columns(GeneratedModel.Of(output), "Doc"));
    }

    [Fact(DisplayName = "B21 an inherited internal setter in the same assembly is a column with no diagnostic")]
    public void An_inherited_internal_setter_in_the_same_assembly_is_mapped()
    {
        var (diagnostics, _, output) = Run(InternalSetter);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        Assert.Equal(["Id", "Code"], Columns(GeneratedModel.Of(output), "Doc"));
    }

    [Fact(DisplayName = "B21 an entity that derives from another entity maps its own and the inherited columns")]
    public void An_entity_of_an_entity_keeps_both_tables()
    {
        var (diagnostics, _, output) = Run(EntityOfEntity);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        var model = GeneratedModel.Of(output);
        Assert.Equal(["Id", "Name"], Columns(model, "Animal"));
        Assert.Equal(["Id", "Name", "Breed"], Columns(model, "Dog"));
    }

    [Fact(DisplayName = "B21 the other side of a many-to-many takes its key type from a base class")]
    public void A_linked_entity_takes_its_key_type_from_its_base()
    {
        var (diagnostics, _, output) = Run(LinkedKey);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        var add = GeneratedModel.Of(output).Class("PostOrm").GetMethod("AddTagAsync")!;
        Assert.Equal(typeof(int), add.GetParameters().Last().ParameterType);
    }

    [Fact(DisplayName = "B21 a class-level attribute of a base class is not inherited by the entity")]
    public void A_class_attribute_on_the_base_is_not_inherited()
    {
        var (diagnostics, _, output) = Run(ClassAttributeOnBase);

        Assert.Empty(diagnostics);
        Assert.Empty(CompileErrors(output));
        Assert.Null(GeneratedModel.Of(output).Soft("Doc"));
    }
}
