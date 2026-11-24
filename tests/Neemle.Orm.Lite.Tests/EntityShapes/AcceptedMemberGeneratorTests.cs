using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.EntityShapes.MemberShapeSources;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class AcceptedMemberGeneratorTests
{
    [Theory(DisplayName = "AC5 an accessor the generated code can use gives no diagnostic and the member is a column")]
    [InlineData("public string Title { get; internal set; } = \"\";")]
    [InlineData("public string Title { get; protected internal set; } = \"\";")]
    [InlineData("public string Title { internal get; set; } = \"\";")]
    public void An_internal_accessor_is_mapped(string member)
    {
        var (diagnostics, _, model) = Built(Table(member));

        Assert.Empty(diagnostics);
        Assert.Equal(["Id", "Title"], Columns(model, "A"));
    }

    [Theory(DisplayName = "AC5 a member the generator has always skipped stays skipped with no diagnostic")]
    [InlineData("public string Title { get; } = \"\";")]
    [InlineData("public string Title => \"x\";")]
    [InlineData("internal string Title { get; set; } = \"\";")]
    [InlineData("public string Title = \"\";")]
    [InlineData("public static string Title { get; set; } = \"\";")]
    [InlineData("[NotAColumn] public string Title { get; private set; } = \"\";")]
    [InlineData("[NotAColumn] public static string Title { get; private set; } = \"\";")]
    public void A_skipped_member_stays_unmapped_and_silent(string member)
    {
        var (diagnostics, _, model) = Built(Table(member));

        Assert.Empty(diagnostics);
        Assert.Equal(["Id"], Columns(model, "A"));
    }

    [Fact(DisplayName = "AC5 a view column with a private getter is mapped with no diagnostic")]
    public void A_view_column_with_a_private_getter_is_accepted()
    {
        var (diagnostics, _, model) = Built(View("public string Title { private get; set; } = \"\";"));

        Assert.Empty(diagnostics);
        Assert.Equal(["Id", "Title"], Columns(model, "V"));
    }

    [Fact(DisplayName = "AC5 a navigation with a private getter gives no diagnostic and keeps its loader")]
    public void A_navigation_with_a_private_getter_is_accepted()
    {
        var (diagnostics, _, model) = Built(Pet("public Owner? Owner { private get; set; }"));

        Assert.Empty(diagnostics);
        Assert.Contains("LoadOwnerAsync", Methods(model, "PetOrm"));
    }

    // ORML-17 (B22) changes this AC5 case: a reference navigation whose foreign key is not found used to be
    // dropped without a word ("as before"); the missing loader was invisible. It now gets ORMLITE026 naming the
    // navigation and the key (the accessor problem of the navigation stays unreported: no loader is built anyway).
    [Fact(
        DisplayName = "ORML-17 a reference navigation with a bad setter and no foreign key gets ORMLITE026 only")]
    public void A_navigation_without_a_foreign_key_gets_one_diagnostic()
    {
        var (diagnostics, _, model) = Built(
            Owner + "[Table(\"pets\")] public class Pet { [Key] public Guid Id { get; set; } "
            + "public Owner? Owner { get; private set; } }");

        var refusal = Assert.Single(diagnostics);
        Assert.Equal("ORMLITE026", refusal.Id);
        Assert.Contains("'Pet.Owner'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'OwnerId'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("LoadOwnerAsync", Methods(model, "PetOrm"));
    }

    [Fact(DisplayName = "AC5 a member that already gets ORMLITE001 gets no second diagnostic")]
    public void An_unmapped_type_gets_one_diagnostic_only()
    {
        var (diagnostics, _, model) = Built(Table("public System.TimeSpan Span { get; private set; }"));

        Assert.Equal("ORMLITE001", Assert.Single(diagnostics).Id);
        Assert.Equal(["Id"], Columns(model, "A"));
    }

    [Fact(
        DisplayName = "AC5 a required member of an unmapped type gets ORMLITE001 only and the entity cannot be "
        + "built")]
    public void A_required_unmapped_member_gets_one_diagnostic_only()
    {
        var (diagnostics, _, model) = Built(Table("public required System.TimeSpan Span { get; set; }"));

        Assert.Equal("ORMLITE001", Assert.Single(diagnostics).Id);
        Assert.Contains("Span", MapThrows(model, "A").Message, StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "AC5 required members under a SetsRequiredMembers constructor give no diagnostic and a Map "
        + "that builds")]
    public void The_attribute_makes_required_members_acceptable()
    {
        const string members = "[NotAColumn] public required string Scratch { get; set; } "
            + "public required string Field; "
            + "[System.Diagnostics.CodeAnalysis.SetsRequiredMembers] public A() { Scratch = \"\"; Field = \"\"; }";

        var (diagnostics, generated, model) = Built(Table(members));

        Assert.Empty(diagnostics);
        Assert.Equal(["Id"], Columns(model, "A"));
        Assert.DoesNotContain("NotSupportedException", generated, StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "AC5 a view with private-set and init navigations gets no diagnostic and the same Map as "
        + "with plain ones")]
    public void A_view_is_exempt_from_the_navigation_accessor_rules()
    {
        var restricted = "public Guid NavId { get; set; } public Kid? Nav { get; init; } "
            + Kids + " { get; private set; } = new(); "
            + "public System.Collections.Generic.List<Kid> More { get; init; } = new(); "
            + TagLink + Tags + " { get; private set; } = new();";
        var plain = restricted
            .Replace("{ get; private set; }", "{ get; set; }")
            .Replace("{ get; init; }", "{ get; set; }");

        var (diagnostics, generated, _) = Built(ParentViewing(restricted));
        var (_, control, _) = Run(ParentViewing(plain));

        Assert.Empty(diagnostics);
        Assert.Equal(MapOf(control, "PV"), MapOf(generated, "PV"));
    }

    [Fact(DisplayName = "AC5 a view column with a private set still gets the property diagnostic")]
    public void A_view_column_with_a_private_set_is_refused()
    {
        var (diagnostics, _, _) = Run(View("public string Title { get; private set; } = \"\";"));

        Assert.Equal(PropertyId, Assert.Single(diagnostics).Id);
    }
}
