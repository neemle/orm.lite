using Neemle.OrmLite.Nodes;
using Xunit;

namespace Neemle.OrmLite.Ports.Tests;

// The migration identity is computed by the core from the declarations alone. This project loads no shipped provider,
// so the values pinned here are the values of a process without PostgreSQL or SQLite; the same literals are pinned in
// tests/Tests/Nodes/MigrationIdentityUpgradeTests.cs, where both providers are loaded.
public sealed class MigrationIdentityTests
{
    private const string IndexSetHashOfDocs = "b590ddf4256c5bca";

    private const string SetHashOfTenantTable = "991e2994cc4b646b";

    private const string SetHashOfTenantTableUnderAppActor = "c593dc0a15978ec6";

    private static readonly RowHistoryTable TenantTable =
        new RowHistoryTable("corpus_tasks", "id", "deleted_at", "tenant_id", new[] { "updated_at" });

    private static EntityDescriptor Doc() =>
        EntityDescriptor.Of("global::T.Doc", "docs", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("Name", "name", ColumnType.Text, false, false)
            .Property("DeletedAt", "deleted_at", ColumnType.Timestamp, true, false)
            .Index("ix_docs_name", true, true, "name")
            .SoftDelete("DeletedAt", "deleted_at", "doc");

    [Fact(DisplayName = "no shipped provider is registered in this project, so the identity below is the one of a "
        + "process that loaded neither PostgreSQL nor SQLite")]
    public void No_shipped_provider_is_registered()
    {
        Assert.Null(ProviderRegistry.Find("postgres"));
        Assert.Null(ProviderRegistry.Find("sqlite"));
    }

    [Fact(DisplayName = "the index-set identity and the reconcile version name are computed from the declarations "
        + "alone, with no provider registered")]
    public void The_index_set_identity_needs_no_provider()
    {
        var schema = OrmSchema.Of("Tok", Doc());

        Assert.Equal(IndexSetHashOfDocs, schema.IndexSetHash);
        var reconcile = schema.Contributions().Single(
            c => c.Version.StartsWith(OrmSchema.ReconcilePrefix, StringComparison.Ordinal));
        Assert.Equal(OrmSchema.ReconcilePrefix + "Tok_" + IndexSetHashOfDocs, reconcile.Version);
    }

    [Fact(DisplayName = "the history set identity and the history version name are computed from the tables alone, "
        + "with no provider registered")]
    public void The_history_set_identity_needs_no_provider()
    {
        var table = new RowHistoryTable("corpus_tasks", "id", "deleted_at", "tenant_id", new[] { "updated_at" });

        Assert.Equal(SetHashOfTenantTable, RowHistoryDdl.SetHash(new[] { table }));
        var history = OrmSchema.Of("Tok", Doc()).Contributions().Single(
            c => c.Version.StartsWith(RowHistoryDdl.VersionPrefix, StringComparison.Ordinal));
        Assert.Equal(
            RowHistoryDdl.VersionPrefix + "Tok_" + RowHistoryDdl.SetHash(OrmSchema.Of("Tok", Doc()).HistoryTables),
            history.Version);
    }

    [Fact(DisplayName = "the history set identity names the actor setting only when it is not the library default, "
        + "so a changed setting renames the install and the default identity stays the tables alone")]
    public void The_actor_setting_is_part_of_the_history_identity_only_when_it_is_not_the_default()
    {
        var tables = new[] { TenantTable };

        Assert.Equal(SetHashOfTenantTable, RowHistoryDdl.SetHash(OrmLiteSettings.DefaultActorSetting, tables));
        Assert.Equal(SetHashOfTenantTableUnderAppActor, RowHistoryDdl.SetHash("app.actor", tables));
        Assert.NotEqual(RowHistoryDdl.SetHash("app.actor", tables), RowHistoryDdl.SetHash("other.actor", tables));
        Assert.Equal(RowHistoryDdl.SetHash(OrmLiteSettings.ActorSetting, tables), RowHistoryDdl.SetHash(tables));
        Assert.Null(ProviderRegistry.Find("postgres"));
        Assert.Throws<ArgumentException>(() => RowHistoryDdl.SetHash("NoDot", tables));
    }
}
