using System.Text;

namespace Neemle.Orm.Lite.Tests.Corpus.Cases;

internal static class GeneratorCases
{
    private static IReadOnlyList<(string Path, string Text)> Files(string root, params string[] relative)
    {
        var files = new List<(string, string)>();
        foreach (var item in relative)
        {
            var full = Path.Combine(root, item.Replace('/', Path.DirectorySeparatorChar));
            var paths = Directory.Exists(full) ? Directory.GetFiles(full, "*.cs", SearchOption.AllDirectories) : [full];
            foreach (var path in paths)
                files.Add((Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllText(path)));
        }
        return files;
    }

    private static string Token(string assemblyName)
    {
        var sb = new StringBuilder();
        foreach (var ch in assemblyName) if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        var s = sb.Length == 0 ? "App" : sb.ToString();
        if (char.IsDigit(s[0])) s = "_" + s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }

    internal static readonly (GeneratorSet Set, bool Runner)[] Sets =
    [
        (new GeneratorSet("demo", "OrmLiteDemo", r => Files(r, "tests/Neemle.Orm.Lite.Tests/Corpus/GeneratorFixtures/Demo"), GeneratorOptions.Unset(), true), true),
        (new GeneratorSet("golden-fixtures", "OrmLite.GoldenFixtures",
            r => Files(r, "tests/Neemle.Orm.Lite.Tests/OrmLiteGolden/Fixtures/Core", "tests/Neemle.Orm.Lite.Tests/OrmLiteGolden/Fixtures/Golden"), GeneratorOptions.Unset(), true), true),
        (new GeneratorSet("corpus-entities", "CorpusEntities", r => Files(r, "tests/Neemle.Orm.Lite.Tests/Corpus/GeneratorFixtures/Entities"), GeneratorOptions.Unset(), true), true),
        (new GeneratorSet("global-namespace", "CorpusGlobalNamespace", r => Files(r, "tests/Neemle.Orm.Lite.Tests/Corpus/GeneratorFixtures/GlobalNamespace"), GeneratorOptions.Unset(), true), true),
        (new GeneratorSet("tenant-option-default", "TenantOptionDefault", r => Files(r, "tests/Neemle.Orm.Lite.Tests/Corpus/GeneratorFixtures/TenantOption"), GeneratorOptions.Unset(), true), false),
        (new GeneratorSet("tenant-option-scope-id", "TenantOptionScope", r => Files(r, "tests/Neemle.Orm.Lite.Tests/Corpus/GeneratorFixtures/TenantOption"), GeneratorOptions.Tenant("ScopeId"), true), false),
        (new GeneratorSet("tenant-option-invalid", "TenantOptionInvalid", r => Files(r, "tests/Neemle.Orm.Lite.Tests/Corpus/GeneratorFixtures/TenantOption"), GeneratorOptions.Tenant("1bad"), true), false),
        (new GeneratorSet("diagnostics", "CorpusDiagnostics", r => Files(r, "tests/Neemle.Orm.Lite.Tests/Corpus/GeneratorFixtures/Diagnostics"), GeneratorOptions.Unset(), false), false),
    ];

    public static void Register(CorpusRun run)
    {
        var probeText = File.ReadAllText(Path.Combine(run.Root, "tests", "Neemle.Orm.Lite.Tests", "Corpus", "GeneratorFixtures", "Probe.cs"));
        foreach (var (set, runner) in Sets)
        {
            var group = "generator-" + set.Name;
            var withProbe = set with
            {
                Sources = r => set.Execute ? [.. set.Sources(r), ("tests/Neemle.Orm.Lite.Tests/Corpus/GeneratorFixtures/Probe.cs", probeText)] : set.Sources(r),
            };
            GeneratorResult? result = null;
            GeneratorResult Result() => result ??= CorpusGenerator.Run(withProbe, run.Root);

            var hints = Result().Sources.Select(s => s.Hint).ToList();
            foreach (var hint in hints)
            {
                var captured = hint;
                run.Rec.Text(group, captured.ToLowerInvariant(), () => Result().Sources.First(s => s.Hint == captured).Text);
            }
            run.Rec.Text(group, "diagnostics", () => Result().Diagnostics.Count == 0 ? "none\n" : string.Join("\n", Result().Diagnostics) + "\n");
            if (!set.Execute) continue;
            var token = Token(set.AssemblyName);
            run.Rec.Text(group, "compiles-with-the-runtime", () => CorpusGenerator.CompileDiagnostics(Result().Output) is { Length: > 0 } errors ? errors + "\n" : "ok\n");
            run.Rec.Text(group, "executed-schema-contributions-and-registry", () =>
            {
                using var probe = CorpusGenerator.Load(Result().Output);
                return probe.Dump(token);
            });
            if (!runner) continue;
            run.Rec.Text(group, "migration-runner-fresh-database", () =>
            {
                using var probe = CorpusGenerator.Load(Result().Output);
                var log = new RecordingLog();
                var source = new RecordingDataSource(log, "primary");
                probe.RunMigrations(source, token).GetAwaiter().GetResult();
                return log.Render();
            });
            run.Rec.Text(group, "migration-runner-partially-applied", () =>
            {
                using var probe = CorpusGenerator.Load(Result().Output);
                var log = new RecordingLog();
                var applied = probe.Versions(token).Take(2).Select(v => new object?[] { v }).ToArray();
                var script = new RecordingScript().WhenContains("FROM \"schema_migrations\"", ScriptedResult.OfRows([typeof(string)], applied));
                var source = new RecordingDataSource(log, "primary", script);
                probe.RunMigrations(source, token).GetAwaiter().GetResult();
                return log.Render();
            });
        }
    }
}
