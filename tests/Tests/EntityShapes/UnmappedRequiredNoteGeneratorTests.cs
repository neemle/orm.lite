using Xunit;
using static Neemle.OrmLite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.OrmLite.Tests.EntityShapes.MemberShapeSources;

namespace Neemle.OrmLite.Tests.EntityShapes;

public sealed class UnmappedRequiredNoteGeneratorTests
{
    private const string RequiredSentence = "The property is required, so the entity cannot be built from a row";

    private const string Sets = "[System.Diagnostics.CodeAnalysis.SetsRequiredMembers] public A() { Span = default; } ";

    [Fact(
        DisplayName = "ORML-70 ORMLITE001 for a required member of an unmapped type says the entity cannot be built")]
    public void A_required_unmapped_member_says_the_entity_is_unbuildable()
    {
        var (diagnostics, _, model) = Built(Table("public required System.TimeSpan Span { get; set; }"));

        var report = Assert.Single(diagnostics);
        Assert.Equal("ORMLITE001", report.Id);
        Assert.Contains(RequiredSentence, report.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Map throws NotSupportedException", report.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Span", MapThrows(model, "A").Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORML-70 a required unmapped member under [SetsRequiredMembers] gets no unbuildable sentence")]
    public void A_required_unmapped_member_under_the_attribute_has_no_extra_sentence()
    {
        var (diagnostics, _, _) = Built(Table(Sets + "public required System.TimeSpan Span { get; set; }"));

        var report = Assert.Single(diagnostics);
        Assert.Equal("ORMLITE001", report.Id);
        Assert.DoesNotContain(RequiredSentence, report.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORML-70 an unmapped member that is not required keeps the original text")]
    public void A_plain_unmapped_member_keeps_the_original_text()
    {
        var (diagnostics, _, _) = Built(Table("public System.TimeSpan Span { get; set; }"));

        var message = Assert.Single(diagnostics).GetMessage();
        Assert.DoesNotContain("is required", message, StringComparison.Ordinal);
        Assert.EndsWith("so this property is not emitted.", message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORML-70 a required key of an unmapped type gets both sentences")]
    public void A_required_key_gets_both_sentences()
    {
        var (diagnostics, _, _) = Built(Table("[Key] public required System.TimeSpan Other { get; set; }"));

        var message = Assert.Single(diagnostics).GetMessage();
        Assert.Contains("The property is a key", message, StringComparison.Ordinal);
        Assert.Contains(RequiredSentence, message, StringComparison.Ordinal);
    }
}
