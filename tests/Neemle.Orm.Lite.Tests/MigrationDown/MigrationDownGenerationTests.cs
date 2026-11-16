using Neemle.Orm.Lite;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.MigrationDown;

public sealed class MigrationDownGenerationTests
{
    private static readonly string Source =
        MigrationClass("WithDown", "0001_with_down", "Reversible", DownOverride("SELECT 'down'"))
        + MigrationClass("WithoutDown", "0002_without_down", "Reversible")
        + MigrationClass("Irrev", "0003_irreversible", "Irreversible")
        + "public abstract class DownBase : Migration { " + DownOverride("SELECT 'inherited down'") + " }\n"
        + MigrationClass("InheritedDown", "0004_inherited_down", "Reversible", baseType: "DownBase")
        + MigrationClass("OverloadIsNotDown", "0005_overload_is_not_down", "Reversible",
            "public void Down(MigrationBuilder b, int other) => b.Sql(\"SELECT 'overload'\");");

    private static readonly string[] Declared =
        ["0001_with_down", "0002_without_down", "0003_irreversible", "0004_inherited_down", "0005_overload_is_not_down"];

    private static IReadOnlyList<MigrationContribution> Contributions() => GeneratedContributions(Run(Source).Output);

    private static MigrationContribution Of(IReadOnlyList<MigrationContribution> all, string version) =>
        Assert.Single(all, c => c.Version == version);

    private static IReadOnlyList<string> Statements(Action<MigrationBuilder>? action)
    {
        var b = new MigrationBuilder(new PgDdlDialect());
        Assert.NotNull(action);
        action!(b);
        return b.Operations;
    }

    [Fact(DisplayName = "a class that overrides Down reaches its Contribution with a Down that runs the Down body, not the Up body")]
    public void An_override_is_emitted_as_the_contribution_down()
    {
        var entry = Of(Contributions(), "0001_with_down");

        Assert.Equal(["SELECT 'down'"], Statements(entry.Down));
        Assert.Equal(["SELECT 'up'"], entry.Ops());
    }

    [Fact(DisplayName = "a class that does not override Down, an unrelated Down overload, and every generated entry carry a null Down")]
    public void No_override_is_a_null_down()
    {
        var all = Contributions();

        Assert.Null(Of(all, "0002_without_down").Down);
        Assert.Null(Of(all, "0003_irreversible").Down);
        Assert.Null(Of(all, "0005_overload_is_not_down").Down);
        Assert.All(all.Where(c => !Declared.Contains(c.Version)), c => Assert.Null(c.Down));
    }

    [Fact(DisplayName = "an override inherited from an intermediate base class counts as overriding Down")]
    public void An_inherited_override_is_emitted()
    {
        Assert.Equal(["SELECT 'inherited down'"], Statements(Of(Contributions(), "0004_inherited_down").Down));
    }

    [Fact(DisplayName = "the generated RunAsync never calls Down")]
    public void Run_async_never_calls_down()
    {
        var generated = Run(Source).Generated;
        var run = generated[generated.IndexOf("Task RunAsync(", StringComparison.Ordinal)..];

        Assert.DoesNotContain("Down", run, StringComparison.Ordinal);
    }
}
