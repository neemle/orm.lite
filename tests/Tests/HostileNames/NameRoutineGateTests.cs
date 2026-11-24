using Xunit;

namespace Neemle.OrmLite.Tests.HostileNames;

public sealed class NameRoutineGateTests
{
    private const string Provider = "Postgres";

    private static string Source(string file) =>
        File.ReadAllText(Path.Combine(RuntimeSources.LibraryRoot(), "src", Provider, file));

    [Fact(DisplayName = "[heuristic] no template of the PostgreSQL data-literal class wraps a placeholder in "
        + "quote marks, so no name reaches a template unescaped")]
    public void No_template_quotes_a_placeholder_itself()
    {
        var source = Source("PostgresDdlDataLiterals.cs");

        Assert.DoesNotContain("\\\"{", source, StringComparison.Ordinal);
        Assert.DoesNotContain("'{0}'", source, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "[heuristic] the PostgreSQL lowering, catalog and serializers never call string.Format: "
        + "a name is formatted only through PostgresNames")]
    public void Names_are_formatted_only_through_the_one_routine()
    {
        foreach (var file in new[]
        {
            "PostgresLowering.cs", "PostgresCatalog.cs", "PostgresSerializer.Ddl.cs", "PostgresSerializer.History.cs",
            "PostgresSerializer.cs", "PostgresProvider.cs",
        })
            Assert.DoesNotContain("string.Format(", Source(file), StringComparison.Ordinal);
        Assert.DoesNotContain("QuoteMark", Source("PostgresLowering.cs"), StringComparison.Ordinal);
    }
}
