using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace Neemle.OrmLite.Tests.Gates;

public sealed class RawSqlRatchetTests
{
    private const string Baseline = "Gates/legacy-raw-sql.txt";

    private static readonly string[] Scanned = ["src", "demo", "tests", "scripts"];

    private static readonly string[] Skipped = ["Corpus/Goldens", "/bin/", "/obj/"];

    internal static int CountRawUses(string source)
    {
        var count = 0;
        foreach (var call in CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is not MemberAccessExpressionSyntax access) continue;
            if (access.Name.Identifier.ValueText == "Raw" && call.ArgumentList.Arguments.Count == 2) count++;
        }
        return count;
    }

    private static Dictionary<string, int> Counts()
    {
        var root = RuntimeSources.LibraryRoot();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var area in Scanned)
        {
            var directory = Path.Combine(root, area);
            if (!Directory.Exists(directory)) continue;
            foreach (var file in LiteralScan.SourceFiles(directory))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (Skipped.Any(s => ("/" + relative).Contains(s, StringComparison.Ordinal))) continue;
                var hits = CountRawUses(File.ReadAllText(file));
                if (hits > 0) counts[relative] = hits;
            }
        }
        return counts;
    }

    private static Dictionary<string, int> Recorded()
    {
        var path = Path.Combine(RuntimeSources.LibraryRoot(), "tests", "Tests", Baseline);
        return File.ReadAllLines(path).Where(l => l.Length > 0).Select(l => l.Split('|'))
            .ToDictionary(s => s[0], s => int.Parse(s[1], System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal);
    }

    [Fact(DisplayName = "[heuristic] raw SQL entering a migration (a Raw call) is counted per file; no file holds "
        + "more than the recorded baseline, and a file leaves it the moment it is clean")]
    public void Raw_sql_never_grows_beyond_the_baseline()
    {
        var actual = Counts();
        var recorded = Recorded();

        var problems = new List<string>();
        foreach (var (file, count) in actual)
        {
            if (!recorded.TryGetValue(file, out var allowed)) problems.Add(file + ": " + count + " raw use(s), none allowed");
            else if (count > allowed) problems.Add(file + ": " + count + " raw use(s), baseline " + allowed);
            else if (count < allowed) problems.Add(file + ": " + count + " raw use(s), baseline " + allowed + "; lower the baseline");
        }
        foreach (var file in recorded.Keys.Where(f => !actual.ContainsKey(f)))
            problems.Add(file + ": clean now; remove it from the baseline");

        Assert.True(problems.Count == 0,
            string.Join("\n", problems) + "\nbaseline as measured:\n"
            + string.Join("\n", actual.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "|" + p.Value)));
    }

    [Fact(DisplayName = "the shipped library holds no raw SQL use of its own: every baseline entry is a test, a fixture or the demo")]
    public void The_library_itself_uses_no_raw_sql()
    {
        Assert.DoesNotContain(Recorded().Keys, f => f.StartsWith("src/", StringComparison.Ordinal));
        Assert.DoesNotContain(Counts().Keys, f => f.StartsWith("src/", StringComparison.Ordinal));
    }

    private const string PlantedSource = """
        class Planted
        {
            void Up(MigrationBuilder b)
            {
                b.Raw("postgres", "SELECT 1");
                b.Raw("sqlite", $"SELECT {1}");
            }
        }
        """;

    private const string TypedSource = """
        class Typed
        {
            void Up(MigrationBuilder b)
            {
                b.CreateTable("t", t => t.Column("id", ColumnType.Uuid).JsonbColumn("d"));
                b.AddColumn("t", "c", ColumnType.Text);
                b.DropTable("t");
                Raw();
                other.Raw(1);
                other.Raw(1, 2, 3);
            }
        }
        """;

    [Fact(DisplayName = "[heuristic] the scan counts a planted Raw call, whatever its text is made of, and ignores "
        + "the typed operations and a Raw of another shape")]
    public void The_scan_counts_planted_uses()
    {
        Assert.Equal(2, CountRawUses(PlantedSource));
        Assert.Equal(0, CountRawUses(TypedSource));
    }

    [Fact(DisplayName = "no public member of the builders is obsolete: Raw is a documented escape hatch, "
        + "not a deprecated one")]
    public void No_builder_member_is_obsolete()
    {
        var obsolete = new List<string>();
        foreach (var type in new[] { typeof(MigrationBuilder), typeof(TableBuilder) })
            foreach (var method in type.GetMethods())
                if (method.GetCustomAttributes(typeof(ObsoleteAttribute), false).Length > 0)
                    obsolete.Add(type.Name + "." + method.Name);

        Assert.Empty(obsolete);
    }

    [Fact(DisplayName = "the builders take no statement without a provider and no column type or default spelled "
        + "as text; Raw names its provider")]
    public void The_builders_take_no_unowned_text()
    {
        var migration = typeof(MigrationBuilder).GetMethods();
        var table = typeof(TableBuilder).GetMethods();

        Assert.DoesNotContain(migration, m => m.Name == "Sql");
        Assert.DoesNotContain(
            migration, m => m.Name == "AddColumn" && m.GetParameters()[2].ParameterType == typeof(string));
        Assert.DoesNotContain(
            table, m => m.Name == "Column" && m.GetParameters()[1].ParameterType == typeof(string));
        var raw = Assert.Single(migration, m => m.Name == "Raw");
        Assert.Equal(["providerId", "sql"], raw.GetParameters().Select(p => p.Name));
    }

    private static readonly string[] TextNames = ["sql", "text", "type", "defaultSql"];

    private static IEnumerable<MethodInfo> BuilderSurface()
    {
        var builders = new[] { typeof(MigrationBuilder), typeof(TableBuilder) };
        var extensions = typeof(MigrationBuilder).Assembly.GetExportedTypes().Where(t => t.IsAbstract && t.IsSealed)
            .SelectMany(t => t.GetMethods()).Where(m => m.IsDefined(typeof(ExtensionAttribute), false)
                && builders.Contains(m.GetParameters()[0].ParameterType));
        return builders.SelectMany(t => t.GetMethods()).Concat(extensions);
    }

    private static bool TakesText(MethodInfo method) => method.GetParameters()
        .Any(p => p.ParameterType == typeof(string) && TextNames.Contains(p.Name));

    [Fact(DisplayName = "no public builder method, and no extension method over a builder, takes a string parameter "
        + "named sql, text, type or defaultSql, whatever the method is called, except MigrationBuilder.Raw")]
    public void No_builder_method_takes_text_under_any_name()
    {
        var carriers = BuilderSurface().Where(TakesText).Select(m => m.DeclaringType!.Name + "." + m.Name).ToList();

        Assert.Equal(["MigrationBuilder.Raw"], carriers);
    }
}
