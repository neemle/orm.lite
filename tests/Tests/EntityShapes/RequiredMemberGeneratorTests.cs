using Xunit;
using static Neemle.OrmLite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.OrmLite.Tests.EntityShapes.MemberShapeSources;

namespace Neemle.OrmLite.Tests.EntityShapes;

public sealed class RequiredMemberGeneratorTests
{
    private const string InternalEntity =
        "[Table(\"a\")] internal class A { [Key] public Guid Id { get; set; } "
        + "internal required string Hidden { get; set; } }\n";

    private static readonly string RequiredTags =
        TagLink + "public required System.Collections.Generic.List<Tag> Tags { get; set; }";

    [Fact(
        DisplayName = "AC4 a required reference, collection, many-to-many list, unmapped property and field are "
        + "refused once")]
    public void A_required_member_that_a_row_cannot_populate_is_refused()
    {
        var shapes = new (string Source, string Entity, string Member)[]
        {
            (Pet("public required Owner Owner { get; set; }"), "Pet", "Owner"),
            (Parent("public required System.Collections.Generic.List<Kid> Kids { get; set; }"), "Parent", "Kids"),
            (Parent(RequiredTags), "Parent", "Tags"),
            (Table("[NotAColumn] public required string Scratch { get; set; }"), "A", "Scratch"),
            (Table("public required string Field;"), "A", "Field"),
            (InternalEntity, "A", "Hidden"),
            (ParentViewing("public required System.Collections.Generic.List<Kid> Kids { get; set; }"), "PV", "Kids"),
        };

        foreach (var (source, entity, member) in shapes)
        {
            var (diagnostics, _, model) = Built(source);

            var refusal = Assert.Single(diagnostics);
            Assert.Equal(PropertyId, refusal.Id);
            Assert.Contains("'" + entity + "." + member + "'", refusal.GetMessage(), StringComparison.Ordinal);
            Assert.Contains("is required", refusal.GetMessage(), StringComparison.Ordinal);
            Assert.Equal(member, SpanText(refusal));
            Assert.Contains("Query", Methods(model, entity + "Orm"));
        }
    }

    [Fact(
        DisplayName = "AC16 the Map of an entity with a required member that a row cannot populate throws and "
        + "names the entity and the member")]
    public void The_map_of_such_an_entity_throws_a_named_error()
    {
        var (_, _, model) = Built(Parent("public required System.Collections.Generic.List<Kid> Kids { get; set; }"));

        var failure = MapThrows(model, "Parent");

        Assert.Contains("Parent cannot be built from a row", failure.Message, StringComparison.Ordinal);
        Assert.Contains("'Kids'", failure.Message, StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "AC16 the entity keeps every member: its columns, Insert and the keyed members are still "
        + "generated")]
    public void The_entity_keeps_every_other_member()
    {
        var (_, _, model) = Built(Table("[NotAColumn] public required string Scratch { get; set; }"));

        Assert.Equal(["Id"], Columns(model, "A"));
        Assert.Equal(
            ["Column", "DeleteAsync", "GetAsync", "InsertAsync", "Map", "Query", "Values"],
            Methods(model, "AOrm"));
    }

    [Fact(DisplayName = "AC4 a required member the Map can populate as a column is not refused")]
    public void A_required_column_is_not_refused()
    {
        var (diagnostics, generated, _) = Built(Table("public required string Title { get; set; }"));

        Assert.Empty(diagnostics);
        Assert.DoesNotContain("NotSupportedException", generated, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC4 a required column with a private getter is refused once and the entity cannot be built")]
    public void A_required_column_with_a_private_getter_is_refused_once()
    {
        var (diagnostics, _, model) = Built(Table("public required string Title { private get; set; }"));

        Assert.Equal(PropertyId, Assert.Single(diagnostics).Id);
        Assert.Contains("'Title'", MapThrows(model, "A").Message, StringComparison.Ordinal);
    }
}
