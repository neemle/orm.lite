using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.MigrationReversibility;

public sealed class DuplicateDeclarationTests
{
    private const string Reversible = "Reversible";

    private static string Entity(string name, string table, string managed = "") =>
        $"[Table(\"{table}\"{managed})] public class {name} {{ [Key] public Guid Id {{ get; set; }} }}\n";

    [Fact(DisplayName = "ORML-18 two migrations with one version get ORMLITE028 each and none is emitted")]
    public void A_repeated_version_is_refused_and_not_emitted()
    {
        var (diagnostics, _, output) = Run(
            MigrationClass("First", "0100_same", Reversible) + MigrationClass("Second", "0100_same", Reversible)
            + MigrationClass("Other", "0200_other", Reversible));

        var reports = diagnostics.Where(d => d.Id == "ORMLITE028")
            .OrderBy(d => d.GetMessage(), StringComparer.Ordinal)
            .ToList();
        Assert.Equal(2, reports.Count);
        Assert.All(reports, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        Assert.Contains("'Fixture.Second'", reports[0].GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'Fixture.First'", reports[1].GetMessage(), StringComparison.Ordinal);
        var versions = GeneratedContributions(output).Select(c => c.Version).ToList();
        Assert.DoesNotContain("0100_same", versions);
        Assert.Contains("0200_other", versions);
    }

    [Theory(DisplayName = "ORML-18 an empty or blank version gets ORMLITE029 and the migration is not emitted")]
    [InlineData("")]
    [InlineData("  ")]
    public void An_empty_version_is_refused(string version)
    {
        var (diagnostics, _, output) = Run(MigrationClass("Blank", version, Reversible));

        var refusal = Assert.Single(diagnostics, d => d.Id == "ORMLITE029");
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains("'Fixture.Blank'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(GeneratedContributions(output), c => c.Version == version);
    }

    [Fact(DisplayName = "ORML-18 distinct versions give neither diagnostic")]
    public void Distinct_versions_are_accepted()
    {
        var (diagnostics, _, output) = Run(
            MigrationClass("A", "0100_a", Reversible) + MigrationClass("B", "0100_b", Reversible));

        Assert.DoesNotContain(diagnostics, d => d.Id is "ORMLITE028" or "ORMLITE029");
        var versions = GeneratedContributions(output).Select(c => c.Version).ToList();
        Assert.Contains("0100_a", versions);
        Assert.Contains("0100_b", versions);
    }

    [Fact(DisplayName = "ORML-18 two managed entities on one table get ORMLITE027 each, naming both")]
    public void A_table_mapped_twice_by_managed_entities_is_refused()
    {
        var (diagnostics, _, _) = Run(Entity("Left", "shared") + Entity("Right", "shared") + Entity("Alone", "alone"));

        var reports = diagnostics.Where(d => d.Id == "ORMLITE027").ToList();
        Assert.Equal(2, reports.Count);
        Assert.All(reports, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
        foreach (var report in reports)
        {
            Assert.Contains("'Fixture.Left', 'Fixture.Right'", report.GetMessage(), StringComparison.Ordinal);
            Assert.Contains("Table 'shared'", report.GetMessage(), StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "ORML-18 a table shared with an unmanaged entity is accepted")]
    public void A_table_shared_with_an_unmanaged_entity_is_accepted()
    {
        var (diagnostics, _, _) = Run(Entity("Left", "shared") + Entity("Right", "shared", ", Managed = false"));

        Assert.DoesNotContain(diagnostics, d => d.Id == "ORMLITE027");
    }

    [Fact(DisplayName = "ORML-18 a view named like a managed table is accepted")]
    public void A_view_may_share_a_table_name()
    {
        var (diagnostics, _, _) = Run(
            Entity("Left", "shared") + "[View(\"shared\")] public class Seen { public Guid Id { get; set; } }\n");

        Assert.DoesNotContain(diagnostics, d => d.Id == "ORMLITE027");
    }
}
