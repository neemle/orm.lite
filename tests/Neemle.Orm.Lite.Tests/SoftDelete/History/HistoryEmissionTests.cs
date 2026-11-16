using Neemle.Orm.Lite;
using Xunit;
using static Neemle.Orm.Lite.Tests.MigrationReversibility.MigrationGeneratorRun;

namespace Neemle.Orm.Lite.Tests.SoftDelete.History;

public sealed class HistoryEmissionTests
{
    private const string Plain = "[Key] public Guid Id { get; set; } public string Name { get; set; } = \"\"; public DateTime? DeletedAt { get; set; }";

    private const string Scope = " public Guid ScopeId { get; set; }";

    private const string HistoryPrefix = "9900_orm_history_reconcile_";

    private static string Entity(string type, string table, string attribute, string members) =>
        $"[Table(\"{table}\")] {attribute} public class {type} {{ {members} }}\n";

    private static string HistoryLine(string source)
    {
        var (diagnostics, generated, _) = Run(source);
        Assert.Empty(diagnostics);
        var start = generated.IndexOf("HistoryTables = ", StringComparison.Ordinal);
        Assert.True(start >= 0, "no HistoryTables member");
        return generated.Substring(start, generated.IndexOf('\n', start) - start).TrimEnd('\r');
    }

    private static string Literal(string table, string key, string soft, string tenant, string unversioned) =>
        "HistoryTables = new global::Neemle.Orm.Lite.RowHistoryTable[] { new global::Neemle.Orm.Lite.RowHistoryTable(\""
        + table + "\", \"" + key + "\", \"" + soft + "\", \"" + tenant + "\", " + unversioned + ") };";

    [Fact(DisplayName = "AC1: a soft entity with the default tenant emits one HistoryTables element naming its key, soft and tenant columns")]
    public void A_default_tenant_entity_is_emitted()
    {
        var line = HistoryLine(Entity("Doc", "docs", "[SoftDelete]", Plain + Scope));

        Assert.Equal(Literal("docs", "id", "deleted_at", "scope_id", "new string[0]"), line);
    }

    [Fact(DisplayName = "AC9: a Platform entity emits an empty tenant column")]
    public void A_platform_entity_emits_an_empty_tenant()
    {
        var line = HistoryLine(Entity("Doc", "docs", "[SoftDelete(Tenant = SoftDeleteAttribute.Platform)]", Plain + Scope));

        Assert.Equal(Literal("docs", "id", "deleted_at", "", "new string[0]"), line);
    }

    [Fact(DisplayName = "AC9: the key, soft, tenant and Unversioned columns are emitted as database column names")]
    public void Columns_are_emitted_as_database_names()
    {
        var line = HistoryLine(Entity("Doc", "docs", "[SoftDelete(\"RemovedAt\", Tenant = \"OwnerId\", Unversioned = new[] { \"Secret\", \"Token\" })]",
            "[Key] [Column(\"doc_id\")] public Guid Id { get; set; } [Column(\"owner_ref\")] public Guid OwnerId { get; set; }"
            + " public string Secret { get; set; } = \"\"; [Column(\"api_token\")] public string Token { get; set; } = \"\"; public DateTime? RemovedAt { get; set; }"));

        Assert.Equal(Literal("docs", "doc_id", "removed_at", "owner_ref", "new string[] { \"secret\", \"api_token\" }"), line);
    }

    [Fact(DisplayName = "AC1: several soft entities are emitted in ordinal table order whatever their declaration order")]
    public void Entities_are_emitted_in_table_order()
    {
        var line = HistoryLine(
            Entity("Zeta", "zetas", "[SoftDelete]", Plain + Scope) + Entity("Alpha", "alphas", "[SoftDelete]", Plain + Scope));

        Assert.True(line.IndexOf("\"alphas\"", StringComparison.Ordinal) < line.IndexOf("\"zetas\"", StringComparison.Ordinal));
        Assert.Equal(2, line.Split("new global::Neemle.Orm.Lite.RowHistoryTable(", StringSplitOptions.None).Length - 1);
    }

    [Fact(DisplayName = "AC10: an assembly with no soft entity emits no HistoryTables member and no history contribution")]
    public void A_non_soft_assembly_is_unchanged()
    {
        var (diagnostics, generated, output) = Run(Entity("Doc", "docs", "", Plain + Scope));

        Assert.Empty(diagnostics);
        Assert.DoesNotContain("HistoryTables", generated, StringComparison.Ordinal);
        Assert.DoesNotContain(HistoryPrefix, generated, StringComparison.Ordinal);
        var (contributions, tables) = Load(output);
        Assert.DoesNotContain(contributions, c => c.Version.StartsWith(HistoryPrefix, StringComparison.Ordinal));
        Assert.Null(tables);
    }

    [Fact(DisplayName = "AC1: a soft assembly gains exactly one Irreversible history contribution with no Down, versioned by the run-time hash of its emitted tables")]
    public void A_soft_assembly_gains_one_history_contribution()
    {
        var (diagnostics, _, output) = Run(Entity("Doc", "docs", "[SoftDelete]", Plain + Scope));
        Assert.Empty(diagnostics);

        var (contributions, tables) = Load(output);

        var history = Assert.Single(contributions, c => c.Version.StartsWith(HistoryPrefix, StringComparison.Ordinal));
        Assert.Equal(Reversibility.Irreversible, history.Reversibility);
        Assert.Null(history.Down);
        Assert.Equal(HistoryPrefix + output.AssemblyName + "_" + RowHistoryDdl.SetHash(tables!), history.Version);
        Assert.Equal(RowHistoryDdl.Reconcile(tables!), history.Ops());
    }

    [Fact(DisplayName = "TD3: a soft entity registers its key column, and a non-soft entity registers none")]
    public void The_key_column_is_registered_on_soft_entities_only()
    {
        var (_, soft, _) = Run(Entity("Doc", "docs", "[SoftDelete]", Plain + Scope));
        var (_, plain, _) = Run(Entity("Doc", "docs", "", Plain + Scope));

        Assert.Contains(", KeyColumn: \"id\"", RegisterLine(soft), StringComparison.Ordinal);
        Assert.DoesNotContain("KeyColumn", RegisterLine(plain), StringComparison.Ordinal);
    }

    private static string RegisterLine(string generated)
    {
        var start = generated.IndexOf("OrmMeta.Register<global::Fixture.Doc>(", StringComparison.Ordinal);
        return generated.Substring(start, generated.IndexOf('\n', start) - start);
    }

    private static (MigrationContribution[] Contributions, IReadOnlyList<RowHistoryTable>? Tables) Load(Microsoft.CodeAnalysis.Compilation output)
    {
        using var stream = new MemoryStream();
        var emitted = output.Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)));
        stream.Position = 0;
        var assembly = System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromStream(stream);
        var migrations = assembly.GetType("Neemle.Orm.Lite.Generated." + output.AssemblyName + "Migrations", throwOnError: true)!;
        var schema = assembly.GetType("Neemle.Orm.Lite.Generated." + output.AssemblyName + "Schema", throwOnError: true)!;
        return ((MigrationContribution[])migrations.GetField("Contributions")!.GetValue(null)!,
            (IReadOnlyList<RowHistoryTable>?)schema.GetField("HistoryTables")?.GetValue(null));
    }
}
