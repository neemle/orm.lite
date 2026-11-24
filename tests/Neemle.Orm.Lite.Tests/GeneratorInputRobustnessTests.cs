using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests;

public sealed class GeneratorInputRobustnessTests
{
    private const string Up = "public override void Up(MigrationBuilder b) { }";

    private static string Stats(string index, string members = "public int Stars { get; set; }") =>
        "[Table(\"stats\")] " + index + " public class Stat { [Key] public Guid Id { get; set; } " + members + " }\n";

    private static string Shaped(string modifiers, string name, string baseClause, string members) =>
        "[Migration(\"0100_x\", Reversibility.Reversible)] " + modifiers + " class " + name + baseClause
        + " { " + members + " }\n";

    private static void AssertGeneratorRan(System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics)
    {
        Assert.DoesNotContain(diagnostics, d => d.Id is "CS8785" or "ORMLITE031");
    }

    [Fact(DisplayName = "ORML-20 a null array of index terms on a class gives ORMLITE020 and does not throw")]
    public void A_null_term_array_on_a_class_is_reported()
    {
        var (diagnostics, _, output) = Run(Stats("[Index((string[]?)null)]"));

        AssertGeneratorRan(diagnostics);
        var problem = Assert.Single(diagnostics, d => d.Id == "ORMLITE020");
        Assert.Contains("it names no column", problem.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(CompileErrors(output));
    }

    [Fact(DisplayName = "ORML-20 a null array of index terms on a property is read as no terms and does not throw")]
    public void A_null_term_array_on_a_property_is_accepted()
    {
        var (diagnostics, _, output) = Run(Stats("", "[Index((string[]?)null)] public int Stars { get; set; }"));

        AssertGeneratorRan(diagnostics);
        Assert.DoesNotContain(diagnostics, d => d.Id == "ORMLITE020");
        Assert.Empty(CompileErrors(output));
    }

    [Fact(DisplayName = "ORML-20 a Where number beyond 64 bits gives ORMLITE020 and does not throw")]
    public void An_overflowing_where_number_is_reported()
    {
        var (diagnostics, _, output) = Run(Stats("[Index(\"Stars\", Where = \"Stars > 99999999999999999999\")]"));

        AssertGeneratorRan(diagnostics);
        var problem = Assert.Single(diagnostics, d => d.Id == "ORMLITE020");
        Assert.Contains("does not fit a 64-bit integer", problem.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(CompileErrors(output));
    }

    [Fact(DisplayName = "ORML-20 the largest 64-bit number is accepted in a Where condition")]
    public void The_largest_long_is_accepted()
    {
        var (diagnostics, _, output) = Run(Stats("[Index(\"Stars\", Where = \"Stars > 9223372036854775807\")]"));

        AssertGeneratorRan(diagnostics);
        Assert.DoesNotContain(diagnostics, d => d.Id == "ORMLITE020");
        Assert.Empty(CompileErrors(output));
    }

    [Fact(DisplayName = "ORML-20 a decimal number in a Where condition is refused with a message that says so")]
    public void A_decimal_where_number_is_refused_clearly()
    {
        var (diagnostics, _, _) = Run(Stats(
            "[Index(\"Price\", Where = \"Price > 9.99\")]", "public decimal Price { get; set; }"));

        var problem = Assert.Single(diagnostics, d => d.Id == "ORMLITE020");
        Assert.Contains("compares with a decimal number", problem.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("cannot read the condition", problem.GetMessage(), StringComparison.Ordinal);
    }

    [Theory(DisplayName = "ORML-20 a [Migration] class the generated code cannot build gets ORMLITE030, not emitted")]
    [InlineData("public abstract", "Abs", " : Migration", Up, "it is abstract")]
    [InlineData("public sealed", "Plain", "", "", "does not derive from Neemle.Orm.Lite.Migration")]
    [InlineData("public static", "Stat", "", "", "it is a static class")]
    [InlineData("public sealed", "Gen<T>", " : Migration", Up, "it is generic")]
    [InlineData("public sealed", "Priv", " : Migration", Up + " private Priv() { }", "no public constructor")]
    [InlineData("public sealed", "Intern", " : Migration", Up + " internal Intern() { }", "no public constructor")]
    [InlineData("public sealed", "Param", " : Migration", Up + " public Param(int x) { }", "no public constructor")]
    public void An_unusable_migration_class_is_reported(
        string modifiers, string name, string baseClause, string members, string reason)
    {
        var (diagnostics, generated, output) = Run(Shaped(modifiers, name, baseClause, members));

        AssertGeneratorRan(diagnostics);
        var refusal = Assert.Single(diagnostics, d => d.Id == "ORMLITE030");
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Contains(reason, refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("0100_x", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("0100_x", generated, StringComparison.Ordinal);
        Assert.Empty(CompileErrors(output));
    }

    [Fact(DisplayName = "ORML-20 a [Migration] class nested in a private way gets ORMLITE030")]
    public void A_private_nested_migration_is_reported()
    {
        var (diagnostics, generated, output) = Run(
            "public class Outer { [Migration(\"0100_x\", Reversibility.Reversible)] private sealed class Nest : "
            + "Migration { " + Up + " } }\n");

        var refusal = Assert.Single(diagnostics, d => d.Id == "ORMLITE030");
        Assert.Contains("which the generated code cannot name", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain("0100_x", generated, StringComparison.Ordinal);
        Assert.Empty(CompileErrors(output));
    }

    [Fact(DisplayName = "ORML-20 a [Migration] record is reported and does not break the generated file")]
    public void A_migration_record_is_reported()
    {
        var (diagnostics, generated, output) = Run(
            "[Migration(\"0100_x\", Reversibility.Reversible)] public record Rec;\n");

        Assert.Single(diagnostics, d => d.Id == "ORMLITE030");
        Assert.DoesNotContain("0100_x", generated, StringComparison.Ordinal);
        Assert.Empty(CompileErrors(output));
    }

    [Fact(DisplayName = "ORML-20 a usable [Migration] class gets no ORMLITE030 and is emitted")]
    public void A_usable_migration_class_is_emitted()
    {
        var (diagnostics, generated, _) = Run(Shaped("public sealed", "Fine", " : Migration", Up));

        Assert.DoesNotContain(diagnostics, d => d.Id == "ORMLITE030");
        Assert.Contains("0100_x", generated, StringComparison.Ordinal);
    }
}
