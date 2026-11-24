using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.OrmLite.Tests.CompositeKey.CompositeKeySources;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.CompositeKey;

public sealed class CompositeKeyRefusalTests
{
    private const string RefusalId = "ORMLITE022";

    private static Diagnostic Refusal(string source)
    {
        var (diagnostics, _, _) = Run(source);
        return Assert.Single(diagnostics, d => d.Id == RefusalId);
    }

    private static int Occurrences(string text, string part) => text.Split(part).Length - 1;

    [Theory(
        DisplayName = "AC2 [SEC] ORMLITE022 refuses an entity with more than one key property, once, as an "
        + "Error, naming the entity and every key in member order and marking a key that is a key by name only")]
    [InlineData(Line, "Line", "OrderId, ProductId", 0)]
    [InlineData(CodeAndId, "Coded", "Code, Id (by name)", 1)]
    [InlineData(ThreeKeys, "Slot", "RegionId, SiteId, SlotId", 0)]
    [InlineData(TwoKeysAndId, "Stamp", "Region, Id (by name), Code", 1)]
    [InlineData(KeyIdAndOther, "Twin", "Id, OtherId", 0)]
    public void A_second_key_is_refused_naming_the_entity_and_every_key(
        string source, string entity, string keys, int byName)
    {
        var refusal = Refusal(source);

        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Equal("OrmLite", refusal.Descriptor.Category);
        Assert.True(refusal.Descriptor.IsEnabledByDefault);
        var message = refusal.GetMessage();
        Assert.Contains("'" + entity + "'", message, StringComparison.Ordinal);
        Assert.Contains("(" + keys + ")", message, StringComparison.Ordinal);
        Assert.Equal(byName, Occurrences(message, "by name"));
    }

    [Theory(DisplayName = "AC2 the diagnostic sits on the class identifier, not on its attributes")]
    [InlineData(Line, "Line")]
    [InlineData(CodeAndId, "Coded")]
    [InlineData(SoftTwoKeys, "SoftPair")]
    public void The_refusal_is_located_on_the_class_identifier(string source, string entity)
    {
        var location = Refusal(source).Location;

        Assert.Equal(entity, location.SourceTree!.GetText().ToString(location.SourceSpan));
    }

    [Theory(
        DisplayName = "AC2 the refusal text has no newline, backtick, pipe or brace, so it can stand in the "
        + "golden and in the corpus index")]
    [InlineData(Line)]
    [InlineData(CodeAndId)]
    [InlineData(SoftTwoKeys)]
    public void The_refusal_text_is_plain_one_line_text(string source)
    {
        var message = Refusal(source).GetMessage();

        Assert.DoesNotContain('\n', message);
        Assert.DoesNotContain('\r', message);
        Assert.DoesNotContain('`', message);
        Assert.DoesNotContain('|', message);
        Assert.DoesNotContain('{', message);
        Assert.DoesNotContain('}', message);
    }

    [Fact(
        DisplayName = "AC2 the fix advice never uses the by-name marker, so the marker counts only keys that are "
        + "keys by their name")]
    public void The_advice_does_not_repeat_the_marker()
    {
        var message = Refusal(Line).GetMessage();

        Assert.DoesNotContain("by name", message, StringComparison.Ordinal);
        Assert.Contains("[Key]", message, StringComparison.Ordinal);
        Assert.Contains("Query().Where(", message, StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "AC5 [SEC] a [SoftDelete] entity with two keys gets both ORMLITE008 with its text "
        + "unchanged and ORMLITE022")]
    public void A_composite_soft_entity_gets_both_refusals()
    {
        var (diagnostics, _, _) = Run(SoftTwoKeys);

        var soft = Assert.Single(diagnostics, d => d.Id == "ORMLITE008");
        Assert.Equal(
            "[SoftDelete] on 'SoftPair' requires exactly one non-nullable System.Guid key property, "
            + "but it has 2 (Id, OtherId)",
            soft.GetMessage());
        var composite = Assert.Single(diagnostics, d => d.Id == RefusalId);
        Assert.Contains("'SoftPair'", composite.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("(Id, OtherId)", composite.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("by name", composite.GetMessage(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "AC2 a source with two refused entities reports one ORMLITE022 for each")]
    public void Each_refused_entity_gets_its_own_refusal()
    {
        var (diagnostics, _, _) = Run(Line + "\n" + ThreeKeys);

        var refusals = diagnostics.Where(d => d.Id == RefusalId).Select(d => d.GetMessage()).ToList();
        Assert.Equal(2, refusals.Count);
        Assert.Single(refusals, m => m.Contains("'Line'", StringComparison.Ordinal));
        Assert.Single(refusals, m => m.Contains("'Slot'", StringComparison.Ordinal));
    }
}
