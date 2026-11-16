using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.MigrationReversibility;

public sealed class UnnamedReversibilityRefusalTests
{
    private static string Planted(string argument) => @"
[Migration(""0001_planted"", " + argument + @")]
public sealed class Planted : Migration
{
    public override void Up(MigrationBuilder b) => b.Sql(""SELECT 1"");
}";

    [Theory(DisplayName = "ORMLITE006 refuses a [Migration] Reversibility argument that is not a named member, naming the class")]
    [InlineData("default", "0")]
    [InlineData("(Reversibility)0", "0")]
    [InlineData("(Reversibility)7", "7")]
    [InlineData("(Reversibility)(-1)", "-1")]
    public void An_unnamed_value_is_a_build_error_naming_the_class(string argument, string value)
    {
        var (diagnostics, generated, _) = Run(Planted(argument));

        var refusal = Assert.Single(diagnostics, d => d.Id == "ORMLITE006");
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        var message = refusal.GetMessage();
        Assert.Contains("Fixture.Planted", message);
        Assert.Contains("0001_planted", message);
        Assert.Contains("'" + value + "'", message);
        Assert.DoesNotContain("0001_planted", generated, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "ORMLITE006 is silent for each named member and the generated entry carries that member")]
    [InlineData("Reversibility.Reversible")]
    [InlineData("Reversibility.Irreversible")]
    public void A_named_member_is_accepted(string argument)
    {
        var (diagnostics, generated, output) = Run(Planted(argument));

        Assert.DoesNotContain(diagnostics, d => d.Id == "ORMLITE006");
        Assert.Empty(output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Contains("new(\"0001_planted\", global::Neemle.Orm.Lite." + argument + ",", generated, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "an unnamed value refuses only its own migration: the named one beside it is still generated")]
    public void The_refusal_does_not_take_a_neighbour_with_it()
    {
        var (diagnostics, generated, _) = Run(@"
[Migration(""0001_refused"", default)]
public sealed class Refused : Migration { public override void Up(MigrationBuilder b) => b.Sql(""SELECT 1""); }

[Migration(""0002_kept"", Reversibility.Irreversible)]
public sealed class Kept : Migration { public override void Up(MigrationBuilder b) => b.Sql(""SELECT 1""); }");

        var refusal = Assert.Single(diagnostics, d => d.Id == "ORMLITE006");
        Assert.Contains("Fixture.Refused", refusal.GetMessage());
        Assert.DoesNotContain("Fixture.Kept", refusal.GetMessage());
        Assert.DoesNotContain("0001_refused", generated, StringComparison.Ordinal);
        Assert.Contains("new(\"0002_kept\", global::Neemle.Orm.Lite.Reversibility.Irreversible,", generated, StringComparison.Ordinal);
    }
}
