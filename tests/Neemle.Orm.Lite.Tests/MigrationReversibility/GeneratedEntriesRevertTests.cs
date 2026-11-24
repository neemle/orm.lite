using Neemle.Orm.Lite.Nodes;
using Neemle.Orm.Lite.Postgres;
using Neemle.Orm.Lite.Tests.Settings;
using Neemle.Orm.Lite.Tests.SqliteRules;
using Xunit;

namespace Neemle.Orm.Lite.Tests.MigrationReversibility;

[Collection(ActorSettingCollection.Name)]
public sealed class GeneratedEntriesRevertTests
{
    private const string ReconcilePrefix = "9900_orm_index_reconcile_";

    private static EntityDescriptor Doc(bool soft)
    {
        var doc = EntityDescriptor.Of("global::T.Doc", "docs", true)
            .Property("Id", "id", ColumnType.Uuid, false, true)
            .Property("Name", "name", ColumnType.Text, false, false)
            .Property("DeletedAt", "deleted_at", ColumnType.Timestamp, true, false)
            .Index("ix_docs_name", true, false, "name");
        return soft ? doc.SoftDelete("DeletedAt", "deleted_at", "doc") : doc;
    }

    private const string BaselinePrefix = "0000_baseline_";

    private static ReversibilityFinding AuditOf(MigrationContribution c) => MigrationOps.Audit(
        c.Nodes!(PostgresProvider.Instance), c.Reversibility, hasDownOverride: c.Down is not null);

    [Theory(DisplayName = "every generated entry that carries operations declares what its operations can derive, "
        + "except the baseline, which is declared Irreversible on purpose although its derived Down is invertible")]
    [InlineData(false, 3)]
    [InlineData(true, 4)]
    public void Every_generated_entry_is_audited_clean(bool soft, int expectedEntries)
    {
        using var scope = ActorSettingScope.Fresh();
        var entries = OrmSchema.Of("Tok", Doc(soft)).Contributions().Where(c => c.Nodes is not null).ToList();

        var baseline = Assert.Single(entries, c => c.Version.StartsWith(BaselinePrefix, StringComparison.Ordinal));
        Assert.Equal(expectedEntries, entries.Count);
        Assert.Equal(ReversibilityFinding.DeclaredIrreversibleButInvertible, AuditOf(baseline));
        var others = entries.Where(c => c.Version != baseline.Version);
        Assert.All(others, c => Assert.Equal(ReversibilityFinding.None, AuditOf(c)));
    }

    [Fact(DisplayName = "reverting the generated baseline is refused by name and drops no table, row or history row")]
    public async Task The_baseline_entry_cannot_be_reverted()
    {
        using var scope = ActorSettingScope.Fresh();
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        var ctx = temp.Context;
        var entries = OrmSchema.Of("Tok", Doc(false)).Contributions();
        await MigrationRunner.RunAsync(ctx.DataSource, ctx.Provider, entries);
        await temp.ExecuteAsync("INSERT INTO \"docs\" (\"id\", \"name\") VALUES ('k', 'kept')");
        var before = await SnapshotAsync(temp);
        var baseline = Assert.Single(entries, c => c.Version.StartsWith(BaselinePrefix, StringComparison.Ordinal));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => MigrationRunner.RevertAsync(ctx.DataSource, ctx.Provider, baseline));

        Assert.Contains(baseline.Version, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("Irreversible", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE", before, StringComparison.Ordinal);
        Assert.Equal(before, await SnapshotAsync(temp));
        Assert.Equal("kept", await temp.ScalarAsync("SELECT \"name\" FROM \"docs\" WHERE \"id\" = 'k'"));
    }

    [Fact(DisplayName = "reverting the generated index reconciliation is refused by name "
        + "and changes no table, index or history row")]
    public async Task The_reconcile_entry_cannot_be_reverted()
    {
        using var scope = ActorSettingScope.Fresh();
        await using var temp = await TempSqlite.OpenAsync(schema: false);
        var ctx = temp.Context;
        var entries = OrmSchema.Of("Tok", Doc(false)).Contributions();
        await MigrationRunner.RunAsync(ctx.DataSource, ctx.Provider, entries);
        var before = await SnapshotAsync(temp);
        var reconcile = Assert.Single(entries, c => c.Version.StartsWith(ReconcilePrefix, StringComparison.Ordinal));

        var refusal = await Assert.ThrowsAsync<InvalidOperationException>(
            () => MigrationRunner.RevertAsync(ctx.DataSource, ctx.Provider, reconcile));

        Assert.Contains(reconcile.Version, refusal.Message, StringComparison.Ordinal);
        Assert.Contains("Irreversible", refusal.Message, StringComparison.Ordinal);
        Assert.Contains("ix_docs_name", before, StringComparison.Ordinal);
        Assert.Equal(before, await SnapshotAsync(temp));
    }

    private static async Task<string> SnapshotAsync(TempSqlite temp)
    {
        var objects = await temp.ScalarAsync(
            "SELECT group_concat(type || ' ' || name || ' ' || coalesce(sql, ''), char(10)) "
            + "FROM (SELECT * FROM sqlite_master ORDER BY type, name)");
        var history = await temp.ScalarAsync(
            "SELECT group_concat(version, char(10)) FROM (SELECT version FROM schema_migrations ORDER BY version)");
        return objects + "\n" + history;
    }
}
