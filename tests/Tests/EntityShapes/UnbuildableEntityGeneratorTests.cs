using Microsoft.CodeAnalysis;
using Xunit;
using static Neemle.OrmLite.Tests.EntityShapes.EntityShapeRun;
using static Neemle.OrmLite.Tests.EntityShapes.MemberShapeSources;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.EntityShapes;

public sealed class UnbuildableEntityGeneratorTests
{
    private const string Abstract = "[Table(\"a\")] public abstract class A { [Key] public Guid Id { get; set; } }\n";

    private const string OnlyParameterised =
        "[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } public A(int seed) { } }\n";

    private const string PrivateConstructor =
        "[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } private A() { } }\n";

    private const string ProtectedConstructor =
        "[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } protected A() { } }\n";

    private const string PrivateProtectedConstructor =
        "[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } private protected A() { } }\n";

    private const string PositionalRecord = "[Table(\"a\")] public record A([property: Key] Guid Id, string Title);\n";

    private const string PrimaryConstructor =
        "[Table(\"a\")] public class A(int seed) { [Key] public Guid Id { get; set; } }\n";

    [Theory(DisplayName = "AC8 T fires once for a type the generated code cannot construct, on the type identifier")]
    [InlineData(Abstract, "abstract")]
    [InlineData(OnlyParameterised, "constructor")]
    [InlineData(PrivateConstructor, "constructor")]
    [InlineData(ProtectedConstructor, "constructor")]
    [InlineData(PrivateProtectedConstructor, "constructor")]
    [InlineData(PositionalRecord, "constructor")]
    [InlineData(PrimaryConstructor, "constructor")]
    public void An_unconstructible_type_is_refused_once(string source, string reason)
    {
        var (diagnostics, _, output) = Run(source);

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(TypeId, refusal.Id);
        Assert.Equal(DiagnosticSeverity.Error, refusal.Severity);
        Assert.Equal("OrmLite", refusal.Descriptor.Category);
        Assert.Contains("'A'", refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Contains(reason, refusal.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("A", SpanText(refusal));
        AssertPlainMessage(refusal.GetMessage());
        Assert.Empty(CompileErrors(output));
    }

    [Theory(
        DisplayName = "AC16 the Map of an unconstructible type throws NotSupportedException naming the entity, "
        + "every other member exists")]
    [InlineData(Abstract, "it is abstract")]
    [InlineData(OnlyParameterised, "no accessible constructor")]
    [InlineData(PrivateConstructor, "no accessible constructor")]
    [InlineData(PositionalRecord, "no accessible constructor")]
    public void The_map_of_an_unconstructible_type_throws(string source, string reason)
    {
        var (_, _, output) = Run(source);
        var model = GeneratedModel.Of(output);

        var failure = MapThrows(model, "A");

        Assert.Contains("A cannot be built from a row", failure.Message, StringComparison.Ordinal);
        Assert.Contains(reason, failure.Message, StringComparison.Ordinal);
        Assert.Contains("Query", Methods(model, "AOrm"));
        Assert.Contains("InsertAsync", Methods(model, "AOrm"));
    }

    [Theory(DisplayName = "AC8 a constructor the generated code can call is not refused")]
    [InlineData("[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } public A() { } }\n")]
    [InlineData("[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } internal A() { } }\n")]
    [InlineData("[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } protected internal A() { } }\n")]
    [InlineData("[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } }\n")]
    [InlineData("[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } public A(int seed = 0) { } }\n")]
    [InlineData(
        "[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } public A(params int[] seeds) { } }\n")]
    [InlineData(
        "[Table(\"a\")] public class A { [Key] public Guid Id { get; set; } public A(int seed) { } public A() { } }\n")]
    [InlineData("[Table(\"a\")] public record A { [Key] public Guid Id { get; init; } }\n")]
    [InlineData("[Table(\"a\")] public record A([property: Key] Guid Id) { public A() : this(Guid.Empty) { } }\n")]
    [InlineData("[Table(\"a\")] public record A([property: Key] Guid Id = default, string Title = \"\");\n")]
    [InlineData("[Table(\"a\")] public class A(int seed = 0) { [Key] public Guid Id { get; set; } }\n")]
    public void A_callable_constructor_is_not_refused(string source)
    {
        var (diagnostics, generated, _) = Built(source);

        Assert.Empty(diagnostics);
        Assert.DoesNotContain("NotSupportedException", generated, StringComparison.Ordinal);
    }

    [Theory(
        DisplayName = "AC16 an unconstructible entity as a relation, many-to-many and collection target, in both "
        + "directions, gives one T and nothing else")]
    [InlineData("public abstract class Target", "")]
    [InlineData("public class Target", "public Target(int seed) { }")]
    [InlineData("public class Target", "private Target() { }")]
    public void An_unconstructible_target_gives_one_diagnostic_and_compiles(string header, string constructor)
    {
        var (diagnostics, _, output) = Run(Targets(header, constructor));

        var refusal = Assert.Single(diagnostics);
        Assert.Equal(TypeId, refusal.Id);
        var model = GeneratedModel.Of(output);
        Assert.Contains(
            "Target cannot be built from a row",
            MapThrows(model, "Target").Message,
            StringComparison.Ordinal);
        Assert.Contains("Query", Methods(model, "TargetOrm"));
        Assert.Contains("LoadTargetAsync", Methods(model, "SourceOrm"));
        Assert.Contains("LoadTargetsAsync", Methods(model, "SourceOrm"));
        Assert.Contains("LoadMatesAsync", Methods(model, "SourceOrm"));
    }

    [Fact(
        DisplayName = "AC8 a positional record with a default parameter list is built through its callable "
        + "constructor and the initializer")]
    public void A_positional_record_with_defaults_is_built_by_the_initializer()
    {
        var (_, generated, _) = Built(
            "[Table(\"a\")] public record A([property: Key] Guid Id = default, string Title = \"\");\n");

        Assert.Contains(
            "new global::Fixture.A() { Id = r.GetGuid(o0), Title = r.GetString(o1) }",
            MapOf(generated, "A"),
            StringComparison.Ordinal);
    }
}
