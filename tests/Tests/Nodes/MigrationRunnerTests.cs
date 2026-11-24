using System.Text;
using System.Text.RegularExpressions;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Tests.Corpus;
using Neemle.OrmLite.Tests.Settings;
using Xunit;

namespace Neemle.OrmLite.Tests.Nodes;

[Collection(ActorSettingCollection.Name)]
public sealed class MigrationRunnerTests
{
    private const string Golden = "tests/Tests/Nodes/LegacyRunner/";

    private sealed record Block(string Kind, string Sql, string? Version);

    private static string Pinned(string name) =>
        File.ReadAllText(Path.Combine(RuntimeSources.LibraryRoot(), (Golden + "corpus-entities." + name).Replace('/', Path.DirectorySeparatorChar)));

    private static List<Block> Parse(string transcript)
    {
        var blocks = new List<Block>();
        foreach (var chunk in Regex.Split(transcript.TrimEnd('\n'), "\n(?=#\\d+ )"))
        {
            var lines = chunk.Split('\n');
            var kind = lines[0].Split(' ')[1];
            var sql = new StringBuilder();
            string? version = null;
            var inSql = false;
            foreach (var line in lines.Skip(1))
            {
                if (line == "    sql:") inSql = true;
                else if (inSql && line.StartsWith("        ", StringComparison.Ordinal)) sql.Append(sql.Length == 0 ? "" : "\n").Append(line[8..]);
                else if (line.StartsWith("    @v = String \"", StringComparison.Ordinal)) version = line["    @v = String \"".Length..^1];
                else inSql = false;
            }
            blocks.Add(new Block(kind, sql.ToString(), version));
        }
        return blocks;
    }

    private static List<MigrationContribution> ContributionsOf(string transcript)
    {
        var result = new List<MigrationContribution>();
        var ops = new List<string>();
        var inside = false;
        foreach (var block in Parse(transcript))
        {
            if (block.Kind == "BEGIN")
            {
                inside = true;
                ops = [];
            }
            else if (inside && block.Sql.StartsWith("INSERT INTO schema_migrations", StringComparison.Ordinal))
            {
                var captured = ops.ToList();
                result.Add(new MigrationContribution(block.Version!, Reversibility.Reversible, _ => captured, null));
                inside = false;
            }
            else if (inside) ops.Add(block.Sql);
        }
        return result;
    }

    private static string Normalised(string transcript) =>
        string.Join("\n", Regex.Replace(transcript, " tx=(true|false)", " tx=*").Split('\n').Select(line =>
        {
            var text = line.Trim();
            if (!text.Contains("schema_migrations", StringComparison.Ordinal)) return line;
            if (text.StartsWith("CREATE TABLE", StringComparison.Ordinal)) return "        <ensure the history table>";
            if (text.StartsWith("INSERT", StringComparison.Ordinal)) return "        <record the version>";
            return "        <read the applied versions>";
        }));

    private static string WithTheLockFirst(string normalised)
    {
        const string Ensure = "        <ensure the history table>";
        const string Acquire = "        SELECT pg_advisory_lock(727274)";
        var lines = normalised.Split('\n');
        var ensure = Array.IndexOf(lines, Ensure);
        var acquire = Array.IndexOf(lines, Acquire);
        Assert.True(ensure >= 0 && ensure < acquire);
        (lines[ensure], lines[acquire]) = (lines[acquire], lines[ensure]);
        return string.Join("\n", lines);
    }

    private static async Task<string> RunAsync(IEnumerable<MigrationContribution> contributions, RecordingScript? script = null)
    {
        var log = new RecordingLog();
        var source = new RecordingDataSource(log, "primary", script);
        await MigrationRunner.RunAsync(source, PostgresProvider.Instance, contributions);
        return log.Render();
    }

    [Fact(DisplayName = "on a fresh database the core runner issues the statements of the generated runner in the same "
        + "order except that the lock now precedes the history table: stamp, lock, history table, read, one "
        + "transaction per version, unlock; only the three bookkeeping statements are spelled differently and each "
        + "operation is bound to its transaction explicitly")]
    public async Task The_core_runner_follows_the_generated_runner_on_a_fresh_database()
    {
        using var scope = ActorSettingScope.Fresh();
        var pinned = Pinned("migration-runner-fresh-database.golden");

        var live = await RunAsync(ContributionsOf(pinned));

        Assert.Equal(WithTheLockFirst(Normalised(pinned)), Normalised(live));
        Assert.Equal(8, ContributionsOf(pinned).Count);
    }

    [Fact(DisplayName = "against a partly applied database the core runner skips the applied versions and runs the rest, as the generated runner does")]
    public async Task The_core_runner_skips_applied_versions()
    {
        using var scope = ActorSettingScope.Fresh();
        var all = ContributionsOf(Pinned("migration-runner-fresh-database.golden"));
        var pinned = Pinned("migration-runner-partially-applied.golden");
        var applied = all.OrderBy(c => c.Version, StringComparer.Ordinal).Take(2).Select(c => new object?[] { c.Version }).ToArray();
        var script = new RecordingScript().WhenContains("schema_migrations", ScriptedResult.OfRows([typeof(string)], applied));

        var live = await RunAsync(all, script);

        Assert.Equal(WithTheLockFirst(Normalised(pinned)), Normalised(live));
    }

    [Fact(DisplayName = "versions run in ordinal order whatever order they are supplied in, a blank operation is skipped, and each version is recorded with its own parameter")]
    public async Task Versions_run_in_ordinal_order()
    {
        using var scope = ActorSettingScope.Fresh();
        MigrationContribution Of(string version, params string[] ops) => new(version, Reversibility.Reversible, _ => ops, null);

        var live = await RunAsync([Of("0200_b", "SELECT 2", "  "), Of("0100_a", "SELECT 1")]);

        var blocks = Parse(live);
        var sql = blocks.Select(b => b.Sql).Where(s => s.Length > 0).ToList();
        Assert.True(sql.IndexOf("SELECT 1") < sql.IndexOf("SELECT 2"));
        Assert.DoesNotContain("  ", sql);
        Assert.Equal(["0100_a", "0200_b"], blocks.Where(b => b.Version is not null && b.Sql.StartsWith("INSERT", StringComparison.Ordinal)).Select(b => b.Version));
        Assert.Contains("INSERT INTO \"schema_migrations\" (\"version\") VALUES (@v)", sql);
    }

    [Fact(DisplayName = "a version whose operations fail is not committed and the lock is released")]
    public async Task A_failing_version_releases_the_lock()
    {
        using var scope = ActorSettingScope.Fresh();
        var log = new RecordingLog();
        var source = new RecordingDataSource(log, "primary");
        var failing = new MigrationContribution("0100_a", Reversibility.Reversible, _ => throw new InvalidOperationException("boom"), null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => MigrationRunner.RunAsync(source, PostgresProvider.Instance, [failing]));

        Assert.Equal("boom", error.Message);
        var text = log.Render();
        Assert.DoesNotContain("COMMIT", text, StringComparison.Ordinal);
        Assert.Contains("pg_advisory_unlock(727274)", text.Split("pg_advisory_lock(727274)")[1], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "a provider without session locks is refused by name before the connection is opened")]
    public async Task A_provider_without_locks_is_refused_before_connecting()
    {
        var log = new RecordingLog();
        var source = new RecordingDataSource(log, "primary");

        var error = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => MigrationRunner.RunAsync(source, new NoLockProvider(), []));

        Assert.Equal(Capability.SessionLock, error.Capability);
        Assert.Empty(log.Lines);
    }

    private sealed class NoLockProvider : ISqlProvider
    {
        public string Id => "nolock";

        public CapabilitySet Capabilities { get; } = CapabilitySet.Everything().With(Capability.SessionLock, Support.None);

        public IMigrationPlanner Planner => new PostgresMigrationPlanner(this);

        public ISerializer CreateSerializer() => new PostgresSerializer();
    }
}
