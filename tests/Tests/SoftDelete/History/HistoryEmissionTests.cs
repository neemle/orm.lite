using Neemle.OrmLite;
using Neemle.OrmLite.Postgres;
using Xunit;
using static Neemle.OrmLite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.OrmLite.Tests.SoftDelete.History;

public sealed class HistoryEmissionTests
{
    private const string Plain = "[Key] public Guid Id { get; set; } public string Name { get; set; } = \"\"; public DateTime? DeletedAt { get; set; }";

    private const string Scope = " public Guid ScopeId { get; set; }";

    private const string HistoryPrefix = "9900_orm_history_reconcile_";

    private static string Entity(string type, string table, string attribute, string members) =>
        $"[Table(\"{table}\")] {attribute} public class {type} {{ {members} }}\n";

    private static IReadOnlyList<RowHistoryTable> Tables(string source)
    {
        var (diagnostics, _, output) = Run(source);
        Assert.Empty(diagnostics);
        return GeneratedModel.Of(output).Schema.HistoryTables;
    }

    [Fact(DisplayName = "AC1: a soft entity with the default tenant emits one history table naming its key, soft and tenant columns")]
    public void A_default_tenant_entity_is_emitted()
    {
        var table = Assert.Single(Tables(Entity("Doc", "docs", "[SoftDelete]", Plain + Scope)));

        Assert.Equal(("docs", "id", "deleted_at", "scope_id"), (table.Table, table.KeyColumn, table.SoftColumn, table.TenantColumn));
        Assert.Empty(table.Unversioned);
    }

    [Fact(DisplayName = "AC9: a Platform entity emits an empty tenant column")]
    public void A_platform_entity_emits_an_empty_tenant()
    {
        var table = Assert.Single(Tables(Entity("Doc", "docs", "[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]", Plain + Scope)));

        Assert.Equal(("docs", "id", "deleted_at", ""), (table.Table, table.KeyColumn, table.SoftColumn, table.TenantColumn));
        Assert.Empty(table.Unversioned);
    }

    [Fact(DisplayName = "AC9: the key, soft, tenant and Unversioned columns are emitted as database column names")]
    public void Columns_are_emitted_as_database_names()
    {
        var table = Assert.Single(Tables(Entity("Doc", "docs", "[SoftDelete(\"RemovedAt\", Tenant = \"OwnerId\", Unversioned = new[] { \"Secret\", \"Token\" })]",
            "[Key] [Column(\"doc_id\")] public Guid Id { get; set; } [Column(\"owner_ref\")] public Guid OwnerId { get; set; }"
            + " public string Secret { get; set; } = \"\"; [Column(\"api_token\")] public string Token { get; set; } = \"\"; public DateTime? RemovedAt { get; set; }")));

        Assert.Equal(("docs", "doc_id", "removed_at", "owner_ref"), (table.Table, table.KeyColumn, table.SoftColumn, table.TenantColumn));
        Assert.Equal(new[] { "secret", "api_token" }, table.Unversioned);
    }

    [Fact(DisplayName = "AC1: several soft entities are emitted in ordinal table order whatever their declaration order")]
    public void Entities_are_emitted_in_table_order()
    {
        var tables = Tables(
            Entity("Zeta", "zetas", "[SoftDelete]", Plain + Scope) + Entity("Alpha", "alphas", "[SoftDelete]", Plain + Scope));

        Assert.Equal(new[] { "alphas", "zetas" }, tables.Select(t => t.Table));
    }

    [Fact(DisplayName = "AC10: an assembly with no soft entity has no history tables and no history contribution")]
    public void A_non_soft_assembly_is_unchanged()
    {
        var (diagnostics, _, output) = Run(Entity("Doc", "docs", "", Plain + Scope));

        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        Assert.False(model.Schema.HasSoftDeletion);
        Assert.Empty(model.Schema.HistoryTables);
        Assert.DoesNotContain(model.Contributions, c => c.Version.StartsWith(HistoryPrefix, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "AC1: a soft assembly gains exactly one Irreversible history contribution with no Down, versioned by the run-time hash of its emitted tables")]
    public void A_soft_assembly_gains_one_history_contribution()
    {
        var (diagnostics, _, output) = Run(Entity("Doc", "docs", "[SoftDelete]", Plain + Scope));
        Assert.Empty(diagnostics);
        var model = GeneratedModel.Of(output);
        var tables = model.Schema.HistoryTables;

        var history = Assert.Single(model.Contributions, c => c.Version.StartsWith(HistoryPrefix, StringComparison.Ordinal));

        Assert.Equal(Reversibility.Irreversible, history.Reversibility);
        Assert.Null(history.Down);
        Assert.Equal(HistoryPrefix + output.AssemblyName + "_" + RowHistoryDdl.SetHash(tables), history.Version);
        Assert.Equal(RowHistoryDdl.Reconcile(PostgresProvider.Instance, tables), history.Render(PostgresProvider.Instance));
    }

    [Fact(DisplayName = "TD3: a soft entity registers its key column, and a non-soft entity registers none")]
    public void The_key_column_is_registered_on_soft_entities_only()
    {
        var (_, _, soft) = Run(Entity("Doc", "docs", "[SoftDelete]", Plain + Scope));
        var (_, _, plain) = Run(Entity("Doc", "docs", "", Plain + Scope));

        Assert.Equal("id", GeneratedModel.Of(soft).Soft("Doc")!.KeyColumn);
        Assert.Null(GeneratedModel.Of(plain).Soft("Doc"));
    }
}
