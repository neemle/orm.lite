using Neemle.OrmLite.Postgres;
using Xunit;

namespace Neemle.OrmLite.Tests.SoftDelete.History;

public sealed class RowHistoryTenantVocabularyTests
{
    private static readonly string[] RuntimeFilesThatReadTheHistory =
        ["RowHistoryDdl.cs", "SoftRestore.cs", "RowHistoryRevert.cs", "SoftTrashRead.cs", "RowHistoryRead.cs"];

    [Fact(DisplayName = "the row_history table, its index and its trigger name the tenant column tenant_id")]
    public void The_history_schema_uses_the_tenant_vocabulary()
    {
        var ddl = string.Join('\n', RowHistoryDdl.Preamble(PostgresProvider.Instance));

        Assert.Contains("tenant_id uuid NULL", ddl, StringComparison.Ordinal);
        Assert.Contains("ix_row_history_tenant_xact", ddl, StringComparison.Ordinal);
        Assert.Contains("(tenant_id, xact_id)", ddl, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO row_history (tenant_id, source_table,", ddl, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "no runtime file that reads or writes row_history uses the word workspace")]
    public void No_history_code_uses_the_old_vocabulary()
    {
        foreach (var file in RuntimeFilesThatReadTheHistory)
            Assert.DoesNotContain("workspace", File.ReadAllText(RuntimeSources.Of(file)), StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "the restore batch lookup filters row_history on tenant_id")]
    public void The_restore_lookup_filters_on_the_tenant_column()
    {
        var statement = HistoryStatements.Compile(
            Postgres.PostgresProvider.Instance, HistoryStatements.BatchMembers(Guid.NewGuid(), 1, DateTime.UtcNow));

        Assert.Contains("h.tenant_id IS NOT DISTINCT FROM", statement.Text, StringComparison.Ordinal);
    }
}
