using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.OrmLite.Tests.EntityShapes.EntityShapeRun;

namespace Neemle.OrmLite.Tests.EntityShapes;

public sealed class UnmappedKeyGeneratorTests
{
    private const string NoKeyedApi = "Column InsertAsync Map Query Values";

    private static string Lines(string secondKey) =>
        "[Table(\"lines\")] public class Line { [Key] public Guid A { get; set; } " + secondKey
        + " public int Qty { get; set; } }\n";

    [Theory(
        DisplayName = "ORML-60 a second [Key] the generator cannot map gets ORMLITE023 and the keyed api is withheld")]
    [InlineData("[Key] public Guid B { get; }", "no set accessor")]
    [InlineData("[Key] public Guid B { set { } }", "no get accessor")]
    [InlineData("[Key] internal Guid B { get; set; }", "declared internal")]
    [InlineData("[Key] protected Guid B { get; set; }", "declared protected")]
    [InlineData("[Key] private Guid B { get; set; }", "declared private")]
    public void A_skipped_second_key_withholds_the_keyed_api(string secondKey, string cause)
    {
        var (diagnostics, _, model) = Built(Lines(secondKey));

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(PropertyId, refusal.Id);
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'Line.B'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("[Key]", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains(cause, refusal.GetMessage(), StringComparison.Ordinal);
        AssertPlainMessage(refusal.GetMessage());
        Assert.Equal(NoKeyedApi.Split(' '), Methods(model, "LineOrm"));
        Assert.Equal(["A", "Qty"], Columns(model, "Line"));
    }

    [Fact(
        DisplayName = "ORML-60 a second [Key] of an unmapped type keeps ORMLITE001 as the only diagnostic, says "
        + "it is a key, and withholds the keyed api")]
    public void An_unmapped_second_key_withholds_the_keyed_api()
    {
        var (diagnostics, _, model) = Built(Lines("[Key] public System.TimeSpan B { get; set; }"));

        var refusal = Assert.Single(diagnostics);
        Assert.Equal("ORMLITE001", refusal.Id);
        Assert.Contains("The property is a key, so no keyed get, update or delete", refusal.GetMessage());
        Assert.Equal(NoKeyedApi.Split(' '), Methods(model, "LineOrm"));
    }

    [Fact(DisplayName = "ORML-60 an unmapped property that is no key has no key sentence in ORMLITE001")]
    public void An_unmapped_property_that_is_no_key_has_no_key_sentence()
    {
        var (diagnostics, _, _) = Built(Lines("public System.TimeSpan B { get; set; }"));

        var refusal = Assert.Single(diagnostics);
        Assert.Equal("ORMLITE001", refusal.Id);
        Assert.DoesNotContain("is a key", refusal.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORML-60 a get-only [Key] that is the only key is refused and the entity has no keyed api")]
    public void A_lone_get_only_key_is_refused()
    {
        var (diagnostics, _, model) = Built(
            "[Table(\"a\")] public class A { [Key] public Guid Id { get; } "
            + "public string Title { get; set; } = \"\"; }\n");

        Assert.Equal(PropertyId, Assert.Single(diagnostics).Id);
        Assert.Equal(NoKeyedApi.Split(' '), Methods(model, "AOrm"));
    }

    [Fact(DisplayName = "ORML-60 [NotAColumn] on a [Key] stays pinned: no diagnostic, keyed api kept")]
    public void A_key_marked_not_a_column_is_not_a_lost_key()
    {
        var (diagnostics, _, model) = Built(Lines("[NotAColumn] [Key] public Guid B { get; }"));

        Assert.Empty(diagnostics);
        Assert.Contains("GetAsync", Methods(model, "LineOrm"));
        Assert.Contains("DeleteAsync", Methods(model, "LineOrm"));
    }

    [Theory(DisplayName = "ORML-60 a skipped member that is no [Key] stays skipped and silent")]
    [InlineData("public Guid B { get; }")]
    [InlineData("internal Guid B { get; set; }")]
    [InlineData("public static Guid B { get; set; }")]
    public void A_skipped_non_key_stays_silent(string secondMember)
    {
        var (diagnostics, _, model) = Built(Lines(secondMember));

        Assert.Empty(diagnostics);
        Assert.Contains("GetAsync", Methods(model, "LineOrm"));
    }
}
