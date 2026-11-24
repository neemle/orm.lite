using Xunit;
using static Neemle.Orm.Lite.Tests.EntityShapes.EntityShapeRun;

namespace Neemle.Orm.Lite.Tests.EntityShapes;

public sealed class OptionalConstructorGeneratorTests
{
    private const string Sets = "[System.Diagnostics.CodeAnalysis.SetsRequiredMembers] ";

    private static string Entity(string constructors) =>
        "[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } "
        + "public required string Title { get; set; } " + constructors + " }\n";

    [Fact(
        DisplayName = "ORML-69 [SetsRequiredMembers] on an all-optional constructor is honoured: statement form, "
        + "no diagnostic, zero CS errors")]
    public void An_optional_constructor_with_the_attribute_keeps_the_statement_form()
    {
        var (diagnostics, generated, _) = Built(Entity(Sets + "public A(int unused = 0) { Title = \"\"; }"));

        Assert.Empty(diagnostics);
        var map = MapOf(generated, "A");
        Assert.Contains("e.Title = ", map, StringComparison.Ordinal);
        Assert.DoesNotContain("NotSupportedException", map, StringComparison.Ordinal);
    }

    [Fact(
        DisplayName = "ORML-69 the parameterless constructor without the attribute wins over an optional one with "
        + "it: initializer form")]
    public void The_parameterless_constructor_wins_over_an_attributed_optional_one()
    {
        var (diagnostics, generated, _) = Built(
            Entity("public A() { Title = \"\"; } " + Sets + "public A(int unused = 0) { Title = \"\"; }"));

        Assert.Empty(diagnostics);
        var map = MapOf(generated, "A");
        Assert.DoesNotContain("e.Title = ", map, StringComparison.Ordinal);
        Assert.Contains("Title = ", map, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ORML-69 an optional constructor without the attribute keeps the initializer form")]
    public void An_optional_constructor_without_the_attribute_uses_the_initializer()
    {
        var (diagnostics, generated, _) = Built(Entity("public A(int unused = 0) { }"));

        Assert.Empty(diagnostics);
        Assert.DoesNotContain("e.Title = ", MapOf(generated, "A"), StringComparison.Ordinal);
    }
}
