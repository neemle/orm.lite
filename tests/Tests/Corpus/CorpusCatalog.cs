using Neemle.OrmLite.Tests.Corpus.Cases;
using Neemle.OrmLite.Tests.Settings;

namespace Neemle.OrmLite.Tests.Corpus;

public sealed record CorpusResult(IReadOnlyList<CorpusEntry> Entries, string Index);

public static class CorpusCatalog
{
    public const string GoldenDirectory = "tests/Tests/Corpus/Goldens";

    public const string IndexFile = "tests/Tests/Corpus/INDEX.md";

    public const string GoldenSuffix = ".golden";

    public static CorpusResult Capture(string root)
    {
        using var scope = ActorSettingScope.Fresh();
        CorpusWorld.Ensure();
        var recorder = new CorpusRecorder();
        var run = new CorpusRun(recorder, root);
        QueryReadCases.Register(run);
        QueryWriteCases.Register(run);
        QueryUpsertCases.Register(run);
        SoftDeleteCases.Register(run);
        JsonCases.Register(run);
        GroupingCases.Register(run);
        LocksIntervalsCases.Register(run);
        DdlBuilderCases.Register(run);
        BackslashLiteralCases.Register(run);
        DdlHelperCases.Register(run);
        RowHistoryCases.Register(run);
        RefusalCases.Register(run);
        UpsertSetCases.Register(run);
        EnumOrderCases.Register(run);
        ExecutedCases.Register(run);
        ConstantsCases.Register(run);
        GeneratorCases.Register(run);
        var entries = recorder.Entries.OrderBy(e => e.Group, StringComparer.Ordinal).ThenBy(e => e.Name, StringComparer.Ordinal).ToList();
        return new CorpusResult(entries, CorpusIndex.Build(entries));
    }

    public static string PathOf(string root, CorpusEntry entry) =>
        Path.Combine(root, GoldenDirectory.Replace('/', Path.DirectorySeparatorChar), entry.Group, entry.Name + GoldenSuffix);

    public static IEnumerable<string> ExistingGoldens(string root)
    {
        var directory = Path.Combine(root, GoldenDirectory.Replace('/', Path.DirectorySeparatorChar));
        return Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*" + GoldenSuffix, SearchOption.AllDirectories).Select(f => Path.GetRelativePath(directory, f).Replace('\\', '/')).OrderBy(f => f, StringComparer.Ordinal)
            : [];
    }

    public static string RelativeOf(CorpusEntry entry) => entry.Group + "/" + entry.Name + GoldenSuffix;

    public static IReadOnlyList<string> Compare(string root, CorpusResult result)
    {
        var problems = new List<string>();
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in result.Entries)
        {
            expected.Add(RelativeOf(entry));
            var path = PathOf(root, entry);
            if (!File.Exists(path)) { problems.Add(RelativeOf(entry) + ": captured but has no golden"); continue; }
            var pinned = File.ReadAllText(path);
            if (!string.Equals(pinned, entry.Text, StringComparison.Ordinal))
                problems.Add(RelativeOf(entry) + ": " + FirstDifference(pinned, entry.Text));
        }
        foreach (var stale in ExistingGoldens(root).Where(g => !expected.Contains(g)))
            problems.Add(stale + ": pinned but no longer captured");
        var indexPath = Path.Combine(root, IndexFile.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(indexPath)) problems.Add("INDEX.md: missing");
        else if (!string.Equals(File.ReadAllText(indexPath), result.Index, StringComparison.Ordinal))
            problems.Add("INDEX.md: " + FirstDifference(File.ReadAllText(indexPath), result.Index));
        return problems;
    }

    public static void Write(string root, CorpusResult result)
    {
        var directory = Path.Combine(root, GoldenDirectory.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        foreach (var entry in result.Entries)
        {
            var path = PathOf(root, entry);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, entry.Text, new System.Text.UTF8Encoding(false));
        }
        File.WriteAllText(Path.Combine(root, IndexFile.Replace('/', Path.DirectorySeparatorChar)), result.Index, new System.Text.UTF8Encoding(false));
    }

    private static string FirstDifference(string pinned, string captured)
    {
        var a = pinned.Split('\n');
        var b = captured.Split('\n');
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var left = i < a.Length ? a[i] : "<end of file>";
            var right = i < b.Length ? b[i] : "<end of file>";
            if (left != right) return "line " + (i + 1) + " differs; golden: " + Clip(left) + " | captured: " + Clip(right);
        }
        return "differs only in bytes the line split does not show";
    }

    private static string Clip(string text) => text.Length <= 160 ? text : text[..160] + "...";
}
