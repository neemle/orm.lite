using Xunit;

namespace Neemle.OrmLite.Tests.Ports;

// The ratchet of the ports and adapters layout. The provider-neutral assemblies (Runtime and Sql) may not name a
// provider: no provider type, no provider id, no provider SQL text, no provider-only statement. The allow-list in
// core-coupling-allowlist.txt is empty and a line is never added to it. The project
// arrows are frozen too: any edge that is not in ProjectArrows.Frozen is a violation.
public sealed class CoreNeutralityTests
{
    private static readonly string[] RailsPackages = ["Microsoft.NET.Test.Sdk", "xunit", "xunit.runner.visualstudio"];

    private const string CommentOnlySource =
        "// PgDialect, set_config, postgres and LockKey in a comment\nclass Clean { }";

    private const string PlantedSource = """
        class Planted
        {
            // PgDialect and set_config in a comment are prose, not code
            const string Id = "postgres";
            const string Stamp = "SELECT set_config('x', @v, false)";
            const string Prose = "PgDialect is a name inside prose";
            PgThing Make() => new PgThing(ProviderRegistry.PostgresId);
            long Key() => MigrationRunner.LockKey + MigrationRunner.LockKey;
        }
        """;

    private static List<string> Problems(
        SortedDictionary<string, int> actual, SortedDictionary<string, int> recorded)
    {
        var problems = new List<string>();
        foreach (var (key, count) in actual)
        {
            if (!recorded.TryGetValue(key, out var allowed)) problems.Add(key + ": " + count + " use(s), none allowed");
            else if (count > allowed) problems.Add(key + ": " + count + " use(s), allowed " + allowed);
            else if (count < allowed) problems.Add(key + ": " + count + " use(s), allowed " + allowed + "; lower it");
        }
        foreach (var key in recorded.Keys.Where(k => !actual.ContainsKey(k)))
            problems.Add(key + ": no longer in the source; remove the entry");
        return problems;
    }

    [Fact(DisplayName = "the provider-neutral sources name no provider beyond the allow-list, and an entry leaves "
        + "the allow-list the moment its coupling is gone")]
    public void The_core_names_no_provider_beyond_the_allow_list()
    {
        var root = RuntimeSources.LibraryRoot();
        var actual = CoreCouplingScan.OfTree(root);

        var problems = Problems(actual, CoreCouplingAllowList.Read(root));

        Assert.True(
            problems.Count == 0,
            string.Join("\n", problems) + "\nallow-list as measured:\n"
            + string.Join("\n", actual.Select(p => p.Key + "|" + p.Value)));
    }

    [Fact(DisplayName = "every allow-list entry names an existing neutral source file, a known marker and a "
        + "positive count")]
    public void The_allow_list_is_well_formed()
    {
        var root = RuntimeSources.LibraryRoot();
        var markers = CouplingMarkers.Ids();

        foreach (var (key, count) in CoreCouplingAllowList.Read(root))
        {
            var parts = key.Split('|');
            Assert.True(File.Exists(Path.Combine(root, parts[0])), parts[0] + " does not exist");
            Assert.Contains(CoreCouplingScan.Areas, a => parts[0].StartsWith(a + "/", StringComparison.Ordinal));
            Assert.Contains(parts[1], markers);
            Assert.True(count > 0, key + " has a count of " + count);
        }
    }

    [Fact(DisplayName = "the scan reads the neutral assemblies: both are walked and neither is a handful of files")]
    public void The_scan_walks_both_neutral_assemblies()
    {
        var files = CoreCouplingScan.Files(RuntimeSources.LibraryRoot());

        Assert.True(files.Count >= 100, "only " + files.Count + " neutral source files were scanned");
        Assert.Contains(files, f => InArea(f, "/Runtime/"));
        Assert.Contains(files, f => InArea(f, "/Sql/"));
    }

    [Fact(DisplayName = "the scan counts a planted type, id, SQL text and lock key by token, and ignores comments "
        + "and prose that names a provider")]
    public void The_scan_counts_planted_coupling_by_token()
    {
        var counts = CoreCouplingScan.Of(PlantedSource);

        Assert.Equal(
            new[] { "actor-stamp-sql=1", "migration-lock-key=2", "pg-type=2", "postgres-id=2" },
            counts.Select(p => p.Key + "=" + p.Value));
        Assert.Empty(CoreCouplingScan.Of(CommentOnlySource));
    }

    [Fact(DisplayName = "every project reference of the source projects and of the rails project is one of the "
        + "frozen arrows, and no source project is missing from the table")]
    public void The_project_references_follow_the_frozen_arrows()
    {
        var root = RuntimeSources.LibraryRoot();
        var problems = new List<string>();
        foreach (var (project, expected) in ProjectArrows.Frozen)
        {
            var actual = ProjectArrows.ReferencesOf(root, project);
            if (!actual.SequenceEqual(expected))
                problems.Add(project + " references [" + Listed(actual) + "], frozen [" + Listed(expected) + "]");
        }
        var known = ProjectArrows.Frozen.Keys.Where(k => k.StartsWith("src/", StringComparison.Ordinal));
        foreach (var project in ProjectArrows.SourceProjects(root).Except(known))
            problems.Add(project + " is a source project that the frozen table does not list");

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact(DisplayName = "the neutral assemblies name no package, so no driver can reach them")]
    public void The_neutral_assemblies_reference_no_package()
    {
        var root = RuntimeSources.LibraryRoot();

        Assert.Empty(ProjectArrows.PackagesOf(root, "src/Runtime"));
        Assert.Empty(ProjectArrows.PackagesOf(root, "src/Sql"));
    }

    [Fact(DisplayName = "the rails project takes only the test framework packages, never a driver or a provider")]
    public void The_rails_project_takes_only_test_framework_packages()
    {
        var packages = ProjectArrows.PackagesOf(RuntimeSources.LibraryRoot(), "tests/Ports.Tests");

        Assert.Equal(RailsPackages.Order(StringComparer.Ordinal), packages);
    }

    private static bool InArea(string file, string area) =>
        file.Replace('\\', '/').Contains(area, StringComparison.Ordinal);

    private static string Listed(IEnumerable<string> names) => string.Join(", ", names);
}
