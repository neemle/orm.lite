using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Neemle.Orm.Lite.Tests.OrmLiteGolden;

public class OrmLiteGoldenTests
{
    private const string GoldenDirectory = "tests/Neemle.Orm.Lite.Tests/OrmLiteGolden/Expected";
    private const string FixturesDirectory = "tests/Neemle.Orm.Lite.Tests/OrmLiteGolden/Fixtures";
    private const string GoldenSuffix = ".golden";
    private const string RegenerateCommand = RegenerateVariable + "=1 dotnet test --filter FullyQualifiedName~OrmLiteGolden";
    private const string RegenerateVariable = "ORMLITE_GOLDEN_REGENERATE";
    private const string FixtureAssemblyName = "OrmLite.GoldenFixtures";
    private static readonly string[] PinnedFixtureDirectories = { "Core", "Golden" };

    private const string ImplicitUsings =
        "global using System; global using System.Collections.Generic; global using System.IO; "
        + "global using System.Linq; global using System.Net.Http; global using System.Threading; "
        + "global using System.Threading.Tasks;";

    private static readonly MetadataReference[] References =
    [
        .. (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
            .Split(Path.PathSeparator)
            .Where(p => p.Length > 0 && Path.GetFileNameWithoutExtension(p) != "Neemle.Orm.Lite.Tests")
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
    ];

    [Fact]
    public void The_generated_source_of_the_non_soft_fixture_entities_matches_the_golden_byte_for_byte()
    {
        var root = RuntimeSources.LibraryRoot();
        var golden = Path.Combine(root, GoldenDirectory.Replace('/', Path.DirectorySeparatorChar));
        var actual = Generate(root);

        if (Environment.GetEnvironmentVariable(RegenerateVariable) == "1")
        {
            Directory.CreateDirectory(golden);
            foreach (var stale in Directory.GetFiles(golden, "*" + GoldenSuffix)) File.Delete(stale);
            foreach (var (name, text) in actual) File.WriteAllBytes(Path.Combine(golden, name + GoldenSuffix), Utf8(text));
            return;
        }

        var problems = new List<string>();
        var expectedNames = Directory.Exists(golden)
            ? Directory.GetFiles(golden, "*" + GoldenSuffix).Select(f => Path.GetFileName(f)[..^GoldenSuffix.Length]).ToList()
            : new List<string>();
        foreach (var (name, text) in actual)
        {
            var path = Path.Combine(golden, name + GoldenSuffix);
            if (!File.Exists(path)) { problems.Add($"{name}: generated but has no golden file"); continue; }
            var pinned = File.ReadAllBytes(path);
            var generated = Utf8(text);
            if (pinned.AsSpan().SequenceEqual(generated)) continue;
            problems.Add($"{name}: {FirstDifference(Encoding.UTF8.GetString(pinned), text)}");
        }
        foreach (var name in expectedNames.Where(n => actual.All(a => a.Name != n)))
            problems.Add($"{name}: pinned but no longer generated");

        Assert.True(problems.Count == 0,
            "the generated source of the non-soft fixture entities no longer matches " + GoldenDirectory + ":\n  "
            + string.Join("\n  ", problems)
            + "\nIf the generator change is intended, regenerate with `" + RegenerateCommand + "`, review the diff of "
            + GoldenDirectory + " and commit it with the generator change.");
    }

    [Fact]
    public void The_pinned_fixture_directories_hold_no_soft_delete_entity()
    {
        var root = RuntimeSources.LibraryRoot();
        var soft = PinnedSources(root).Where(s => s.Text.Contains("SoftDelete", StringComparison.Ordinal)).Select(s => s.Path).ToList();
        Assert.True(soft.Count == 0, "the golden pins non-soft entities only; these carry [SoftDelete]: " + string.Join(", ", soft));
    }

    private static List<(string Name, string Text)> Generate(string root)
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = PinnedSources(root)
            .OrderBy(s => s.Path, StringComparer.Ordinal)
            .Select(s => CSharpSyntaxTree.ParseText(s.Text, parse, s.Path))
            .Append(CSharpSyntaxTree.ParseText(ImplicitUsings, parse, "ImplicitUsings.cs"))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            FixtureAssemblyName,
            trees,
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var result = CSharpGeneratorDriver
            .Create([new OrmGenerator().AsSourceGenerator()], parseOptions: parse)
            .RunGeneratorsAndUpdateCompilation(compilation, out _, out _)
            .GetRunResult();
        Assert.Empty(result.Diagnostics);
        return result.Results.SelectMany(r => r.GeneratedSources).Select(s => (s.HintName, s.SourceText.ToString())).ToList();
    }

    private static IEnumerable<(string Path, string Text)> PinnedSources(string root) =>
        PinnedFixtureDirectories
            .SelectMany(d => Directory.GetFiles(Path.Combine(root, FixturesDirectory.Replace('/', Path.DirectorySeparatorChar), d), "*.cs", SearchOption.AllDirectories))
            .Select(f => (Path.GetRelativePath(root, f).Replace('\\', '/'), File.ReadAllText(f)));

    private static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    private static string FirstDifference(string pinned, string generated)
    {
        var a = pinned.Split('\n');
        var b = generated.Split('\n');
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var left = i < a.Length ? a[i] : "<end of file>";
            var right = i < b.Length ? b[i] : "<end of file>";
            if (left != right) return $"line {i + 1} differs\n      golden:    {left.TrimEnd('\r')}\n      generated: {right.TrimEnd('\r')}";
        }
        return "differs only in bytes the line split does not show (BOM or line endings)";
    }
}
