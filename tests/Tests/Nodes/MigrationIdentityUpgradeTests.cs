using Neemle.OrmLite.Nodes;
using Neemle.OrmLite.Postgres;
using Neemle.OrmLite.Tests.Corpus;
using Neemle.OrmLite.Tests.Settings;
using Xunit;

namespace Neemle.OrmLite.Tests.Nodes;

// ORML-21 slice S5 (owner decision D1): the version names of the index reconcile and the history install are the
// same whichever providers are loaded, and a PostgreSQL database that carries the names built from rendered text runs
// the renamed installs once and not again.
[Collection(ActorSettingCollection.Name)]
public sealed class MigrationIdentityUpgradeTests
{
    private const string Insert = "INSERT INTO \"schema_migrations\"";

    // The values that tests/Ports.Tests/MigrationIdentityTests.cs pins in a process with no provider.
    private const string IndexSetHashOfDocs = "b590ddf4256c5bca";

    private const string SetHashOfTenantTable = "991e2994cc4b646b";

    private const string SetHashOfTenantTableUnderAppActor = "c593dc0a15978ec6";

    private static EntityDescriptor Doc() =>
        EntityDescriptor.Of("global::T.Doc", "docs", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("TenantId", "tenant_id", ColumnType.Uuid, false, false)
            .Property("Name", "name", ColumnType.Text, false, false)
            .Property("DeletedAt", "deleted_at", ColumnType.Timestamp, true, false)
            .Index("ix_docs_name", true, true, "name")
            .SoftDelete("DeletedAt", "deleted_at", "doc")
            .Tenant("tenant_id");

    private static string OldIndexVersion(OrmSchema schema) =>
        OrmSchema.ReconcilePrefix + schema.Token + "_"
        + OrmSchema.StableHash(DdlCompiler.Render(schema.Indexes(), PostgresProvider.Instance));

    private static string OldHistoryVersion(OrmSchema schema) =>
        RowHistoryDdl.VersionPrefix + schema.Token + "_"
        + RowHistoryDdl.StatementsHash(RowHistoryDdl.Reconcile(PostgresProvider.Instance, schema.HistoryTables));

    private static string VersionStarting(OrmSchema schema, string prefix) =>
        schema.Contributions().Single(c => c.Version.StartsWith(prefix, StringComparison.Ordinal)).Version;

    private static async Task<List<string>> RunAsync(OrmSchema schema, IEnumerable<string> applied)
    {
        var log = new RecordingLog();
        var rows = applied.Select(v => new object?[] { v }).ToArray();
        var script = new RecordingScript()
            .WhenContains("schema_migrations", ScriptedResult.OfRows([typeof(string)], rows));
        var source = new RecordingDataSource(log, "primary", script);
        await MigrationRunner.RunAsync(source, PostgresProvider.Instance, schema.Contributions());
        return log.Lines.ToList();
    }

    private static bool Records(string line, string version) =>
        line.Contains(Insert, StringComparison.Ordinal)
        && line.Contains("@v = String \"" + version + "\"", StringComparison.Ordinal);

    [Fact(DisplayName = "with the PostgreSQL provider loaded the index-set and history identities are "
        + "the values of a process with no provider")]
    public void The_identity_is_the_same_with_the_provider_loaded()
    {
        var table = new RowHistoryTable("corpus_tasks", "id", "deleted_at", "tenant_id", ["updated_at"]);

        Assert.NotNull(ProviderRegistry.Find("postgres"));
        Assert.Equal(IndexSetHashOfDocs, OrmSchema.Of("Tok", Doc()).IndexSetHash);
        Assert.Equal(SetHashOfTenantTable, RowHistoryDdl.SetHash([table]));
        Assert.Equal(SetHashOfTenantTableUnderAppActor, RowHistoryDdl.SetHash("app.actor", [table]));
    }

    [Fact(DisplayName = "the reconcile and history versions are renamed: the names built from rendered "
        + "text differ from the structural ones")]
    public void The_versions_are_renamed()
    {
        var schema = OrmSchema.Of("Tok", Doc());

        Assert.NotEqual(OldIndexVersion(schema), VersionStarting(schema, OrmSchema.ReconcilePrefix));
        Assert.NotEqual(OldHistoryVersion(schema), VersionStarting(schema, RowHistoryDdl.VersionPrefix));
    }

    [Fact(DisplayName = "a database that carries the old reconcile and history names runs the renamed "
        + "installs once, adds one version row for each and leaves the old rows alone")]
    public async Task An_upgraded_database_runs_the_renamed_installs_once()
    {
        using var scope = ActorSettingScope.Fresh();
        var schema = OrmSchema.Of("Tok", Doc());
        var applied = new[]
        {
            "0000_baseline_Tok", "0001_orm_indexes_Tok", OldIndexVersion(schema), OldHistoryVersion(schema),
        };

        var log = await RunAsync(schema, applied);

        var inserts = log.Where(l => l.Contains(Insert, StringComparison.Ordinal)).ToList();
        Assert.Equal(2, inserts.Count);
        Assert.Single(inserts, l => Records(l, VersionStarting(schema, OrmSchema.ReconcilePrefix)));
        Assert.Single(inserts, l => Records(l, VersionStarting(schema, RowHistoryDdl.VersionPrefix)));
        Assert.DoesNotContain(log, l => l.Contains("DELETE FROM \"schema_migrations\"", StringComparison.Ordinal));
        Assert.Contains(log, l => l.Contains("CREATE TABLE IF NOT EXISTS row_history", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "a custom actor setting renames the history version, so the capture function is "
        + "installed again under the new name, and leaves the index reconcile version alone")]
    public void A_custom_actor_setting_renames_the_history_version()
    {
        using var scope = ActorSettingScope.Fresh();
        var schema = OrmSchema.Of("Tok", Doc());
        var tables = schema.HistoryTables;
        var byDefault = RowHistoryDdl.VersionPrefix + "Tok_"
            + RowHistoryDdl.SetHash(OrmLiteSettings.DefaultActorSetting, tables);
        var index = OrmSchema.ReconcilePrefix + "Tok_" + IndexSetHashOfDocs;
        OrmLiteSettings.UseActorSetting("app.actor");

        var history = VersionStarting(schema, RowHistoryDdl.VersionPrefix);

        Assert.Equal(RowHistoryDdl.VersionPrefix + "Tok_" + RowHistoryDdl.SetHash("app.actor", tables), history);
        Assert.NotEqual(byDefault, history);
        Assert.Equal(index, VersionStarting(schema, OrmSchema.ReconcilePrefix));
    }

    [Fact(DisplayName = "the second run after the upgrade is a no-op: no install statement and no version row")]
    public async Task The_second_run_after_the_upgrade_applies_nothing()
    {
        using var scope = ActorSettingScope.Fresh();
        var schema = OrmSchema.Of("Tok", Doc());
        var applied = schema.Contributions().Select(c => c.Version)
            .Append(OldIndexVersion(schema)).Append(OldHistoryVersion(schema));

        var log = await RunAsync(schema, applied);

        Assert.DoesNotContain(log, l => l.Contains(Insert, StringComparison.Ordinal));
        Assert.DoesNotContain(log, l => l.Contains("row_history", StringComparison.Ordinal));
        Assert.DoesNotContain(log, l => l.Contains("$reconcile$", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "the renamed installs are re-runnable statements: every table, function, "
        + "trigger and index they write is created only when missing or replaced in place")]
    public void The_renamed_installs_are_idempotent_statements()
    {
        var schema = OrmSchema.Of("Tok", Doc());
        var history = string.Join("\n", schema.Contributions().Single(
            c => c.Version == VersionStarting(schema, RowHistoryDdl.VersionPrefix)).Render(PostgresProvider.Instance));
        var reconcile = string.Join("\n", schema.Contributions().Single(
            c => c.Version == VersionStarting(schema, OrmSchema.ReconcilePrefix)).Render(PostgresProvider.Instance));

        Assert.Contains("CREATE TABLE IF NOT EXISTS row_history", history, StringComparison.Ordinal);
        Assert.Contains("CREATE OR REPLACE FUNCTION row_history_capture", history, StringComparison.Ordinal);
        Assert.Contains("CREATE OR REPLACE TRIGGER row_history_u", history, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE row_history", history, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX IF NOT EXISTS \"ix_docs_name\"", reconcile, StringComparison.Ordinal);
    }
}
