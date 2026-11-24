using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Ports.Tests.Fake;
using Xunit;

namespace Neemle.OrmLite.Ports.Tests;

public sealed class FakeProviderMigrationTests
{
    private readonly PortsScenario _scenario = new();

    private Task RunAsync(params MigrationContribution[] contributions) =>
        MigrationRunner.RunAsync(_scenario.Source, _scenario.Provider, contributions);

    [Fact(DisplayName = "the runner applies the pending migrations of a third provider in version order "
        + "through the default planner of the core, because the provider supplies none")]
    public async Task Pending_migrations_run_in_version_order_through_the_default_planner()
    {
        await RunAsync(PortsScenario.AddNote(), PortsScenario.CreateWidgets());

        Assert.Equal(new[] { PortsScenario.FirstVersion, PortsScenario.SecondVersion }, _scenario.Database.Applied);
        Assert.Null(((ISqlProvider)_scenario.Provider).Planner);
        var sql = _scenario.Database.Sql();
        var created = IndexOf(sql, "CREATE TABLE IF NOT EXISTS \"" + PortsScenario.Table);
        var altered = IndexOf(sql, "ALTER TABLE \"" + PortsScenario.Table);
        Assert.True(created >= 0, "the widgets table was not created");
        Assert.True(created < altered, "the column was added before the table was created");
    }

    [Fact(DisplayName = "the runner owns the history table: it creates it, reads it and records each version "
        + "with the parameter spelling of the provider")]
    public async Task The_history_table_is_created_read_and_written_with_the_provider_spelling()
    {
        await RunAsync(PortsScenario.CreateWidgets());

        var sql = _scenario.Database.Sql();
        Assert.Contains(sql, s => s.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase)
            && s.Contains("\"schema_migrations\"", StringComparison.Ordinal));
        var record = Assert.Single(
            _scenario.Database.Statements, s => s.Text.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(":v", record.Text, StringComparison.Ordinal);
        Assert.Equal(PortsScenario.FirstVersion, (string)record.Values["v"]);
    }

    [Fact(DisplayName = "the system actor is stamped by the provider, and each migration commits in a "
        + "transaction of its own")]
    public async Task The_actor_is_stamped_through_the_provider_and_each_migration_commits()
    {
        await RunAsync(PortsScenario.CreateWidgets(), PortsScenario.AddNote());

        var events = _scenario.Database.Events;
        Assert.Equal("stamp:system", events[0]);
        Assert.Equal(3, events.Count(e => e == "commit"));
        Assert.DoesNotContain("rollback", events);
    }

    [Fact(DisplayName = "a second run reads the applied versions back and executes none of the migrations again")]
    public async Task A_second_run_applies_nothing()
    {
        await RunAsync(PortsScenario.CreateWidgets(), PortsScenario.AddNote());
        var before = _scenario.Database.Statements.Count;

        await RunAsync(PortsScenario.CreateWidgets(), PortsScenario.AddNote());

        var added = _scenario.Database.Sql().Skip(before).ToList();
        Assert.NotEmpty(added);
        Assert.DoesNotContain(added, s => s.Contains(PortsScenario.Table, StringComparison.Ordinal));
        Assert.Equal(2, _scenario.Database.Applied.Count);
    }

    [Fact(DisplayName = "a version supplied twice is refused before any statement reaches the server")]
    public async Task A_repeated_version_is_refused_before_anything_runs()
    {
        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RunAsync(PortsScenario.CreateWidgets(), PortsScenario.CreateWidgets()));

        Assert.Contains(PortsScenario.FirstVersion, refusal.Message, StringComparison.Ordinal);
        Assert.Empty(_scenario.Database.Statements);
    }

    [Fact(DisplayName = "raw SQL scoped to another provider is refused by the default planner, and nothing "
        + "is recorded")]
    public async Task Raw_sql_of_another_provider_is_refused()
    {
        var foreign = PortsScenario.Contribution("0003_foreign", new RawSql("elsewhere", "select 1"));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync(foreign));

        Assert.Contains("'elsewhere'", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("'" + FakeProvider.ProviderId + "'", refusal.Message, StringComparison.Ordinal);
        Assert.Empty(_scenario.Database.Applied);
    }

    [Fact(DisplayName = "a migration that needs a capability the fake declares unsupported is refused by capability")]
    public async Task A_migration_needing_an_unsupported_capability_is_refused()
    {
        var locking = PortsScenario.Contribution("0004_lock", new SessionLock(SessionLockOp.Wait, new IntLit(1)));

        var refusal = await Assert.ThrowsAsync<CapabilityNotSupportedException>(() => RunAsync(locking));

        Assert.Equal(Capability.SessionLock, refusal.Capability);
        Assert.Equal(FakeProvider.ProviderId, refusal.Provider);
        Assert.Empty(_scenario.Database.Applied);
    }

    private static int IndexOf(IReadOnlyList<string> sql, string prefix)
    {
        for (var i = 0; i < sql.Count; i++)
            if (sql[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }
}
