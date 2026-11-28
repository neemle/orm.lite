using Xunit;

namespace Neemle.OrmLite.Tests.Gates;

// Admitted reviewer test, kept as a regression guard (provenance: ORML-143, §803 FULL, round 1; tracked as ORML-147).
// Committed with its assertions unchanged; only the sibling test the review refused was left out.
public sealed class ComposerUpgradeAttackTests
{
    [Theory]
    [InlineData(
        "using Composer = Neemle.CSharpComposer.Cs; " +
        "var p = Composer.Parse(text);")]
    [InlineData(
        "using static Neemle.CSharpComposer.Cs; " +
        "var p = Parse(text);")]
    [InlineData(
        "var p = Cs/* comment */.Parse(text);")]
    public void F2_Existing_guard_detects_equivalent_parse_calls(
        string planted)
    {
        // Calls the existing assertion, without copying its regex.
        // Expected failure for every supplied example.
        new GeneratorComposerOnlyTests()
            .The_guard_catches_planted_escapes(planted);
    }
}
