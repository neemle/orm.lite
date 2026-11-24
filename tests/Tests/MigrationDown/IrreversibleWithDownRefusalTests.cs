using Neemle.OrmLite;
using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.MigrationDown;

public sealed class IrreversibleWithDownRefusalTests
{
    private static readonly string Both =
        MigrationClass("IrrevWithDown", "0001_irrev_down", "Irreversible", DownOverride("SELECT 2"))
        + MigrationClass("RevWithDown", "0002_reversible_down", "Reversible", DownOverride("SELECT 2"))
        + MigrationClass("IrrevClean", "0003_irrev_clean", "Irreversible");

    [Fact(DisplayName = "ORMLITE003 refuses an Irreversible migration that overrides Down, naming the class and version, and only that one")]
    public void An_irreversible_migration_with_down_is_a_build_error()
    {
        var (diagnostics, _, _) = Run(Both);

        var refusal = Assert.Single(diagnostics, d => d.Id == "ORMLITE003");
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        var message = refusal.GetMessage();
        Assert.Contains("Fixture.IrrevWithDown", message);
        Assert.Contains("0001_irrev_down", message);
        Assert.DoesNotContain("Fixture.RevWithDown", message);
        Assert.DoesNotContain("Fixture.IrrevClean", message);
    }

    [Fact(DisplayName = "the refused migration is still emitted as Irreversible with a null Down, and its neighbours are untouched")]
    public void The_refusal_does_not_emit_a_down_for_an_irreversible_entry()
    {
        var (_, _, output) = Run(Both);
        var all = GeneratedContributions(output);

        var refused = Assert.Single(all, c => c.Version == "0001_irrev_down");
        Assert.Equal(Reversibility.Irreversible, refused.Reversibility);
        Assert.Null(refused.Down);
        Assert.NotNull(Assert.Single(all, c => c.Version == "0002_reversible_down").Down);
        Assert.Null(Assert.Single(all, c => c.Version == "0003_irrev_clean").Down);
    }
}
