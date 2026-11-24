using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Tests.Corpus;
using Neemle.Orm.Lite.Tests.Settings;
using Xunit;

namespace Neemle.Orm.Lite.Tests.Nodes;

[Collection(ActorSettingCollection.Name)]
public sealed class MigrationRunnerLockOrderTests
{
    private const string Stamp = "set_config('ormlite.actor'";
    private const string Acquire = "pg_advisory_lock(727274)";
    private const string Release = "pg_advisory_unlock(727274)";
    private const string Ensure = "CREATE TABLE IF NOT EXISTS \"schema_migrations\"";
    private const string Read = "SELECT \"version\" FROM \"schema_migrations\"";
    private const string Record = "INSERT INTO \"schema_migrations\"";

    private static MigrationContribution Plain(string version, params string[] statements) =>
        new(version, Reversibility.Reversible, _ => statements, null);

    private static MigrationContribution Refused(string version) =>
        MigrationContribution.Of(version, Reversibility.Reversible, [new RawSql("sqlite", "SELECT 1")]);

    private static int At(RecordingLog log, string fragment) =>
        log.Lines.ToList().FindIndex(line => line.Contains(fragment, StringComparison.Ordinal));

    private static void AssertInOrder(RecordingLog log, params string[] fragments)
    {
        var positions = fragments.Select(fragment => At(log, fragment)).ToList();
        Assert.DoesNotContain(-1, positions);
        Assert.Equal(positions.Order().ToList(), positions);
        Assert.Equal(positions.Count, positions.Distinct().Count());
    }

    private static Task RunAsync(RecordingLog log, RecordingScript? script, params MigrationContribution[] all) =>
        MigrationRunner.RunAsync(new RecordingDataSource(log, "primary", script), PostgresProvider.Instance, all);

    [Fact(DisplayName = "on PostgreSQL the advisory lock is taken before the history table is created and read, "
        + "and released only after the last commit")]
    public async Task The_lock_is_taken_before_the_history_table_is_created()
    {
        using var scope = ActorSettingScope.Fresh();
        var log = new RecordingLog();

        await RunAsync(log, null, Plain("0100_a", "SELECT 1"));

        AssertInOrder(log, Stamp, Acquire, Ensure, Read, "BEGIN", Record, "COMMIT", Release);
    }

    [Fact(DisplayName = "on PostgreSQL a failing history table create releases the lock and starts no transaction")]
    public async Task A_failing_bootstrap_releases_the_lock()
    {
        using var scope = ActorSettingScope.Fresh();
        var log = new RecordingLog();
        var failure = new InvalidOperationException("bootstrap failed");
        var script = new RecordingScript().WhenContains(Ensure, ScriptedResult.Failing(failure));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(log, script, Plain("0100_a", "SELECT 1")));

        Assert.Same(failure, error);
        AssertInOrder(log, Acquire, Ensure, Release);
        Assert.Equal(-1, At(log, "BEGIN"));
        Assert.Equal(-1, At(log, "COMMIT"));
    }

    [Fact(DisplayName = "on PostgreSQL a refusal in a later pending migration is raised before any transaction begins, "
        + "so the earlier pending migration is not committed, and the lock is released")]
    public async Task A_later_refusal_is_raised_before_the_first_migration_commits()
    {
        using var scope = ActorSettingScope.Fresh();
        var log = new RecordingLog();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(log, null, Plain("0100_a", "SELECT 1"), Refused("0200_b")));

        Assert.Contains("provider 'sqlite'", error.Message, StringComparison.Ordinal);
        AssertInOrder(log, Acquire, Ensure, Read, Release);
        Assert.Equal(-1, At(log, "BEGIN"));
        Assert.Equal(-1, At(log, "COMMIT"));
        Assert.Equal(-1, At(log, "ROLLBACK"));
        Assert.Equal(-1, At(log, Record));
    }

    [Fact(DisplayName = "on PostgreSQL an applied migration is neither rendered nor validated, a pending one is "
        + "rendered exactly once, and the statements and their order inside the version are unchanged")]
    public async Task Only_pending_migrations_are_rendered_and_each_once()
    {
        using var scope = ActorSettingScope.Fresh();
        var log = new RecordingLog();
        var renders = 0;
        var appliedBefore = new MigrationContribution(
            "0100_a", Reversibility.Reversible, _ => throw new InvalidOperationException("must not render"), null);
        var pending = new MigrationContribution(
            "0200_b", Reversibility.Reversible, _ => { renders++; return ["SELECT 2", "  ", "SELECT 3"]; }, null);
        var applied = ScriptedResult.OfRows([typeof(string)], new object?[] { "0100_a" });
        var script = new RecordingScript().WhenContains(Read, applied);

        await RunAsync(log, script, appliedBefore, pending);

        Assert.Equal(1, renders);
        AssertInOrder(log, Acquire, Ensure, Read, "BEGIN", "SELECT 2", "SELECT 3", Record, "COMMIT", Release);
        Assert.Equal(1, log.Lines.Count(line => line.Contains("BEGIN", StringComparison.Ordinal)));
    }
}
