using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.EntityShapes.MemberShapeSources;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class UnassignableMemberGeneratorTests
{
    private static (string Source, string Entity, string Member, string Loader)[] Navigations(string accessors) =>
    [
        (Parent(Kids + " " + accessors + " = new();"), "Parent", "Kids", "LoadKidsAsync"),
        (Parent(TagLink + Tags + " " + accessors + " = new();"), "Parent", "Tags", "LoadTagsAsync"),
        (Pet("public Owner? Owner " + accessors), "Pet", "Owner", "LoadOwnerAsync"),
    ];

    private static void AssertRefusedNavigation(
        string source, string entity, string member, string loader, string reason)
    {
        var (diagnostics, _, output) = Run(source);

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(PropertyId, refusal.Id);
        Assert.Contains("'" + entity + "." + member + "'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains(reason, refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("is not mapped", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Equal(member, SpanText(refusal));
        var methods = Methods(GeneratedModel.Of(output), entity + "Orm");
        Assert.DoesNotContain(loader, methods);
        Assert.DoesNotContain("AddTagAsync", methods);
        Assert.DoesNotContain("RemoveTagAsync", methods);
    }

    [Theory(DisplayName = "AC4 P fires once for a column whose accessor the generated code cannot use")]
    [InlineData("{ get; private set; }", "set accessor is private")]
    [InlineData("{ get; protected set; }", "set accessor is protected")]
    [InlineData("{ get; private protected set; }", "set accessor is private protected")]
    [InlineData("{ get; private init; }", "init accessor is private")]
    [InlineData("{ private get; set; }", "get accessor is private")]
    [InlineData("{ protected get; set; }", "get accessor is protected")]
    [InlineData("{ private protected get; set; }", "get accessor is private protected")]
    public void A_column_with_an_unusable_accessor_is_refused(string accessors, string reason)
    {
        var (diagnostics, generated, output) = Run(Table("public string Title " + accessors + " = \"\";"));

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(PropertyId, refusal.Id);
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Equal("OrmLite", refusal.Descriptor.Category);
        var message = refusal.GetMessage();
        Assert.Contains("'A.Title'", message, StringComparison.Ordinal);
        Assert.Contains(reason, message, StringComparison.Ordinal);
        Assert.Contains("is not mapped", message, StringComparison.Ordinal);
        Assert.Contains("no loader that reads it is generated", message, StringComparison.Ordinal);
        Assert.Equal("Title", SpanText(refusal));
        Assert.Equal(["Id"], Columns(GeneratedModel.Of(output), "A"));
        Assert.DoesNotContain("Title", generated, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "AC4 the refusal of a column is one line of plain text")]
    [InlineData("{ get; private set; }")]
    [InlineData("{ private get; set; }")]
    public void The_property_refusal_is_plain_text(string accessors)
    {
        var (diagnostics, _, _) = Run(Table("public string Title " + accessors + " = \"\";"));

        AssertPlainMessage(Assert.Single(diagnostics).GetMessage());
    }

    [Theory(DisplayName = "AC4 a view column with an unusable set accessor is refused")]
    [InlineData("{ get; private set; }")]
    [InlineData("{ get; protected set; }")]
    [InlineData("{ get; private protected set; }")]
    public void A_view_column_with_an_unusable_set_is_refused(string accessors)
    {
        var (diagnostics, _, output) = Run(View("public string Title " + accessors + " = \"\";"));

        Assert.Equal(PropertyId, Assert.Single(diagnostics).Id);
        Assert.Equal(["Id"], Columns(GeneratedModel.Of(output), "V"));
    }

    [Theory(
        DisplayName = "AC4 a collection, a many-to-many list and a reference with its key are refused for an "
        + "unusable set accessor")]
    [InlineData("{ get; private set; }", "set accessor is private")]
    [InlineData("{ get; protected set; }", "set accessor is protected")]
    [InlineData("{ get; private protected set; }", "set accessor is private protected")]
    public void A_navigation_with_an_unusable_set_is_refused(string accessors, string reason)
    {
        foreach (var (source, entity, member, loader) in Navigations(accessors))
            AssertRefusedNavigation(source, entity, member, loader, reason);
    }

    [Fact(DisplayName = "AC4 an init-only collection, many-to-many list and reference of a table are refused")]
    public void An_init_only_navigation_is_refused()
    {
        foreach (var (source, entity, member, loader) in Navigations("{ get; init; }"))
            AssertRefusedNavigation(source, entity, member, loader, "init-only");
    }

    [Fact(DisplayName = "AC4 the refusal of a navigation names the fix and is one line of plain text")]
    public void The_navigation_refusal_names_the_fix()
    {
        var (diagnostics, _, _) = Run(Parent(Kids + " { get; init; } = new();"));

        var message = Assert.Single(diagnostics).GetMessage();
        AssertPlainMessage(message);
        Assert.Contains("[NotAColumn]", message, StringComparison.Ordinal);
        Assert.Contains("no loader for it is generated", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC4 a type with only refused members is still constructible: one diagnostic, no throwing Map")]
    public void A_constructible_type_with_a_refused_member_gets_only_the_property_diagnostic()
    {
        var (diagnostics, generated, _) = Built(Table("public string Title { get; private set; } = \"\";"));

        Assert.Equal(PropertyId, Assert.Single(diagnostics).Id);
        Assert.DoesNotContain("NotSupportedException", generated, StringComparison.Ordinal);
    }

    [Theory(
        DisplayName = "AC6 a reference whose foreign key is refused gets no loader and no diagnostic of its own, "
        + "in either order")]
    [InlineData("public Owner? Owner { get; set; } public Guid OwnerId { get; private set; }")]
    [InlineData("public Guid OwnerId { get; private set; } public Owner? Owner { get; set; }")]
    public void A_reference_with_a_refused_key_is_dropped_whatever_the_order(string members)
    {
        var source = Owner + "[Table(\"pets\")] public class Pet { [Key] public Guid Id { get; set; } "
            + members + " }\n";

        var (diagnostics, _, output) = Run(source);

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(PropertyId, refusal.Id);
        Assert.Contains("'Pet.OwnerId'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("LoadOwnerAsync", Methods(GeneratedModel.Of(output), "PetOrm"));
    }

    [Fact(
        DisplayName = "AC7 a [SoftDelete] column with a private set gets the property diagnostic and ORMLITE002 "
        + "with its text unchanged")]
    public void A_soft_delete_column_with_a_private_set_gets_both_diagnostics()
    {
        const string source = "[Table(\"docs\")] [SoftDelete(Tenant = SoftDeleteAttribute.Platform)] public class Doc "
            + "{ [Key] public Guid Id { get; set; } public DateTime? DeletedAt { get; private set; } }\n";

        var (diagnostics, _, output) = Run(source);

        Assert.Equal(2, diagnostics.Length);
        Assert.Single(diagnostics, d => d.Id == PropertyId);
        var soft = Assert.Single(diagnostics, d => d.Id == "ORMLITE002");
        Assert.Equal(
            "[SoftDelete] on 'Doc' names 'DeletedAt', which is excluded from the column set ([NotAColumn], or not "
            + "a public read-write instance property). The soft-delete column must be a mapped System.DateTime? "
            + "property of the entity.",
            soft.GetMessage());
        Assert.Equal(["Id"], Columns(GeneratedModel.Of(output), "Doc"));
    }
}
